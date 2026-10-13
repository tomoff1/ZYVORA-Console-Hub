using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Zyvora.Core;
public record ConsoleProfile(string Host, int FtpPort = 2121, int ManagerPort = 8084, int LoaderPort = 9021, string Firmware = "13.60", string User = "anonymous", string Password = "zyvora@local");
public record RemoteEntry(string Name, bool IsDirectory, long Size);
public record TransferProgress(long Bytes, long Total);
public sealed class FtpSession : IAsyncDisposable
{
    readonly TcpClient control = new();
    StreamReader reader = null!;
    StreamWriter writer = null!;
    public static void ValidatePath(string path) { if (string.IsNullOrEmpty(path) || path.Any(c => c == '\r' || c == '\n' || c == '\0')) throw new ArgumentException("Chemin FTP invalide."); }
    public static async Task<FtpSession> Open(ConsoleProfile profile, CancellationToken ct)
    {
        NetworkTools.RequirePrivateHost(profile.Host);
        var s = new FtpSession();
        try {
            await s.control.ConnectAsync(profile.Host, profile.FtpPort, ct);
            s.reader = new StreamReader(s.control.GetStream(), Encoding.UTF8, false, 4096, true);
            s.writer = new StreamWriter(s.control.GetStream(), new UTF8Encoding(false), 4096, true) { NewLine = "\r\n", AutoFlush = true };
            s.Expect(await s.Reply(ct), 220);
            var response = await s.Command("USER " + profile.User, ct);
            if (response.Code == 331) response = await s.Command("PASS " + profile.Password, ct);
            s.Expect(response, 230);
            s.Expect(await s.Command("TYPE I", ct), 200);
            return s;
        } catch { await s.DisposeAsync(); throw; }
    }
    public async Task<(int Code, string Text)> Command(string command, CancellationToken ct)
    {
        ValidatePath(command);
        await writer.WriteLineAsync(command.AsMemory(), ct);
        return await Reply(ct);
    }
    async Task<(int Code, string Text)> Reply(CancellationToken ct)
    {
        var first = await reader.ReadLineAsync(ct) ?? throw new IOException("Serveur FTP déconnecté.");
        if (first.Length < 3 || !int.TryParse(first[..3], out int code)) throw new IOException("Réponse FTP invalide.");
        var result = new StringBuilder(first);
        if (first.Length > 3 && first[3] == '-') {
            for (int i=0;i<1000;i++) { var line=await reader.ReadLineAsync(ct) ?? throw new IOException("Réponse FTP interrompue."); result.Append('\n').Append(line); if (line.StartsWith(code + " ")) return (code,result.ToString()); }
            throw new IOException("Réponse FTP trop longue.");
        }
        return (code,result.ToString());
    }
    void Expect((int Code,string Text) response, params int[] codes) { if (!codes.Contains(response.Code)) throw new IOException(response.Text); }
    async Task<TcpClient> Data(CancellationToken ct)
    {
        var reply = await Command("EPSV",ct); int port;
        if (reply.Code == 229) { var m=Regex.Match(reply.Text,@"\(\|\|\|(\d+)\|\)"); if(!m.Success) throw new IOException("EPSV invalide."); port=int.Parse(m.Groups[1].Value); }
        else { reply=await Command("PASV",ct); Expect(reply,227); var m=Regex.Match(reply.Text,@"\((\d+),(\d+),(\d+),(\d+),(\d+),(\d+)\)"); if(!m.Success) throw new IOException("PASV invalide."); port=int.Parse(m.Groups[5].Value)*256+int.Parse(m.Groups[6].Value); }
        // Never follow an advertised PASV host to a third-party address.
        var peer=((IPEndPoint)control.Client.RemoteEndPoint!).Address;
        var data=new TcpClient(); try { await data.ConnectAsync(peer,port,ct); return data; } catch { data.Dispose(); throw; }
    }
    public async Task<IReadOnlyList<RemoteEntry>> List(string path, CancellationToken ct)
    {
        ValidatePath(path); string text; bool machine=true;
        using(var data=await Data(ct)) { var response=await Command("MLSD "+path,ct);
            if(response.Code is not (125 or 150)) machine=false;
            if(machine) { using var r=new StreamReader(data.GetStream(),Encoding.UTF8); text=await r.ReadToEndAsync(ct); Expect(await Reply(ct),226,250); }
            else text="";
        }
        if(!machine) { using var data=await Data(ct); Expect(await Command("LIST "+path,ct),125,150); using var r=new StreamReader(data.GetStream(),Encoding.UTF8); text=await r.ReadToEndAsync(ct); Expect(await Reply(ct),226,250); }
        return ParseListing(text,machine);
    }
    public static IReadOnlyList<RemoteEntry> ParseListing(string text,bool machine)
    {
        var rows=new List<RemoteEntry>();
        foreach(var line in text.Split('\n',StringSplitOptions.RemoveEmptyEntries)) {
            var clean=line.TrimEnd('\r'); string name; bool dir; long size=0;
            if(machine) { int split=clean.IndexOf(' '); if(split<0) continue; name=clean[(split+1)..]; var facts=clean[..split].Split(';'); if(facts.Any(f=>f.Equals("type=cdir",StringComparison.OrdinalIgnoreCase)||f.Equals("type=pdir",StringComparison.OrdinalIgnoreCase))) continue; dir=facts.Any(f=>f.Equals("type=dir",StringComparison.OrdinalIgnoreCase)); foreach(var fact in facts) if(fact.StartsWith("size=",StringComparison.OrdinalIgnoreCase)) long.TryParse(fact[5..],out size); }
            else { var m=Regex.Match(clean,@"^([d-])[rwxstST-]{9}\s+\d+\s+\S+\s+\S+\s+(\d+)\s+\S+\s+\d+\s+\S+\s+(.+)$"); if(!m.Success) continue; name=m.Groups[3].Value; dir=m.Groups[1].Value=="d"; size=long.Parse(m.Groups[2].Value); }
            if(name is "." or ".." || name.Contains('/') || name.Contains('\\') || name.Contains('\0')) continue;
            rows.Add(new(name,dir,size));
        }
        return rows.OrderByDescending(e=>e.IsDirectory).ThenBy(e=>e.Name).ToList();
    }
    public async Task Upload(string local,string remote,IProgress<TransferProgress>? progress,CancellationToken ct)
    {
        ValidatePath(remote); using var file=File.OpenRead(local); using var data=await Data(ct);
        Expect(await Command("STOR "+remote,ct),125,150);
        await Copy(file,data.GetStream(),file.Length,progress,ct); data.Close(); Expect(await Reply(ct),226,250);
    }
    public async Task Download(string remote,string local,long size,IProgress<TransferProgress>? progress,CancellationToken ct)
    {
        ValidatePath(remote); string temp=local+"."+Guid.NewGuid().ToString("N")+".partial";
        try { using(var data=await Data(ct)) { Expect(await Command("RETR "+remote,ct),125,150); await using var file=File.Create(temp); await Copy(data.GetStream(),file,size,progress,ct); } Expect(await Reply(ct),226,250); File.Move(temp,local,true); }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    public static async Task Copy(Stream source,Stream destination,long total,IProgress<TransferProgress>? progress,CancellationToken ct,long maxBytes=long.MaxValue)
    { var buffer=new byte[128*1024]; long done=0; int count; while((count=await source.ReadAsync(buffer,ct))>0) { if(count>maxBytes-done)throw new IOException("Limite de taille dépassée.");await destination.WriteAsync(buffer.AsMemory(0,count),ct); done+=count; progress?.Report(new(done,total)); } }
    public async Task Delete(string path,bool directory,CancellationToken ct) { ValidatePath(path); Expect(await Command((directory?"RMD ":"DELE ")+path,ct),250); }
    public async Task MakeDirectory(string path,CancellationToken ct) { ValidatePath(path); Expect(await Command("MKD "+path,ct),257,250); }
    public async Task Rename(string from,string to,CancellationToken ct) { ValidatePath(from);ValidatePath(to);Expect(await Command("RNFR "+from,ct),350);Expect(await Command("RNTO "+to,ct),250); }
    public ValueTask DisposeAsync() { control.Dispose(); reader?.Dispose(); writer?.Dispose(); return ValueTask.CompletedTask; }
}
