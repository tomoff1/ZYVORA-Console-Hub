using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace Zyvora.Core;
public static class NetworkTools
{
    public static bool IsPrivate(IPAddress ip) { var b=ip.GetAddressBytes(); return b.Length==4 && (b[0]==10 || b[0]==192 && b[1]==168 || b[0]==172 && b[1]>=16 && b[1]<=31 || b[0]==127 || b[0]==169 && b[1]==254); }
    public static void RequirePrivateHost(string host) { if(!IPAddress.TryParse(host,out var ip)||!IsPrivate(ip)) throw new ArgumentException("Indiquez une adresse IPv4 locale privée."); }
    public static async Task<string> ProbeFtp(string host,int port,CancellationToken ct)
    { RequirePrivateHost(host); using var c=new TcpClient();await c.ConnectAsync(host,port,ct); using var r=new StreamReader(c.GetStream());var banner=await r.ReadLineAsync(ct); if(banner?.StartsWith("220")!=true) throw new IOException("Ce port ne fournit pas une bannière FTP. Vérifiez le protocole et le port.");return banner; }
    public static IEnumerable<string> LocalCandidates()
    {
        var seen=new HashSet<string>();
        foreach(var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up && n.NetworkInterfaceType!=NetworkInterfaceType.Loopback))
        foreach(var addr in nic.GetIPProperties().UnicastAddresses.Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork && IsPrivate(a.Address))) {
            var bytes=addr.Address.GetAddressBytes(); var mask=addr.IPv4Mask.GetAddressBytes(); if(mask[0]!=255||mask[1]!=255||mask[2]!=255) continue;
            for(int i=1;i<255;i++) { if((i & mask[3])!=(bytes[3] & mask[3])||i==bytes[3]) continue; var host=$"{bytes[0]}.{bytes[1]}.{bytes[2]}.{i}"; if(seen.Add(host)) yield return host; }
        }
    }
    public static async Task<IReadOnlyList<ConsoleProfile>> Discover(CancellationToken ct)
    {
        var found=new System.Collections.Concurrent.ConcurrentBag<ConsoleProfile>();
        await Parallel.ForEachAsync(LocalCandidates(),new ParallelOptions{MaxDegreeOfParallelism=24,CancellationToken=ct},async(host,token)=>{
            foreach(int port in new[]{1337,2121}) { using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(700);try { var banner=await ProbeFtp(host,port,timeout.Token); if(banner.Contains("PS5",StringComparison.OrdinalIgnoreCase)||banner.Contains("ftpsrv",StringComparison.OrdinalIgnoreCase)) {found.Add(new(host,port));break;} } catch(Exception e) when(e is IOException or SocketException or OperationCanceledException){} }
        }); return found.ToList();
    }
    public static async Task SendPayload(ConsoleProfile profile,string file,IProgress<TransferProgress>? progress,CancellationToken ct)
    {
        RequirePrivateHost(profile.Host); if(!new[]{".elf",".bin"}.Contains(Path.GetExtension(file).ToLowerInvariant()))throw new ArgumentException("Seuls les fichiers ELF et BIN sont acceptés.");
        using var tcp=new TcpClient();await tcp.ConnectAsync(profile.Host,profile.LoaderPort,ct);using var input=File.OpenRead(file);await FtpSession.Copy(input,tcp.GetStream(),input.Length,progress,ct);tcp.Client.Shutdown(SocketShutdown.Send);
    }
    public static async Task<string> ManagerVersion(ConsoleProfile profile,CancellationToken ct)
    { RequirePrivateHost(profile.Host);using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(10)};return await http.GetStringAsync($"http://{profile.Host}:{profile.ManagerPort}/version",ct); }
    public static async Task UploadToManager(ConsoleProfile profile,string file,CancellationToken ct)
    {
        RequirePrivateHost(profile.Host);if(!new[]{".elf",".bin"}.Contains(Path.GetExtension(file).ToLowerInvariant()))throw new ArgumentException("Seuls les fichiers ELF et BIN sont acceptés.");
        using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(5)};
        using var input=File.OpenRead(file);using var content=new StreamContent(input);content.Headers.ContentType=new("application/octet-stream");
        using var response=await http.PostAsync($"http://{profile.Host}:{profile.ManagerPort}/manage:upload?filename={Uri.EscapeDataString(Path.GetFileName(file))}",content,ct);response.EnsureSuccessStatusCode();
    }
}
public record CatalogItem(string Id,string Name,string Version,string Url,string Sha256,string License,string Source,string[] Firmwares,string Validation,string Description);
public static class CatalogService
{
    public static IReadOnlyList<CatalogItem> Load(string path) => JsonSerializer.Deserialize<List<CatalogItem>>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true}) ?? [];
    public static bool DeclaredCompatible(CatalogItem item,string firmware)=>item.Firmwares.Contains(firmware) && item.Validation=="upstream-reported";
    public static async Task<string> Hash(string file,CancellationToken ct=default) { await using var stream=File.OpenRead(file);return Convert.ToHexString(await SHA256.HashDataAsync(stream,ct)).ToLowerInvariant(); }
    public static async Task Download(CatalogItem item,string destination,IProgress<TransferProgress>? progress,CancellationToken ct)
    {
        if(!Uri.TryCreate(item.Url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||item.Sha256.Length!=64||!item.Sha256.All(Uri.IsHexDigit)||string.IsNullOrWhiteSpace(item.License)) throw new InvalidDataException("Source HTTPS, licence et SHA-256 requis.");
        var temp=destination+"."+Guid.NewGuid().ToString("N")+".partial";
        try { using var http=new HttpClient(){Timeout=TimeSpan.FromMinutes(30)};using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();if(response.RequestMessage?.RequestUri?.Scheme!="https")throw new IOException("Redirection non HTTPS refusée.");
            const long limit=512L*1024*1024;var length=response.Content.Headers.ContentLength;
            if(length>limit)throw new IOException("Téléchargement catalogue limité à 512 Mio.");
            var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destination))!);
            if(drive.AvailableFreeSpace<(length??limit)+64L*1024*1024)throw new IOException("Espace disque insuffisant pour télécharger et vérifier le fichier.");
            await using(var output=File.Create(temp)) { using var input=await response.Content.ReadAsStreamAsync(ct);await FtpSession.Copy(input,output,length??0,progress,ct,limit); }
            if(!string.Equals(await Hash(temp,ct),item.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA-256 incorrect : fichier rejeté.");File.Move(temp,destination,true);
        } finally { if(File.Exists(temp))File.Delete(temp); }
    }
}
public static class Diagnostics
{
    public static string Analyze(string text)
    {
        var tips=new List<string>();var t=text.ToLowerInvariant();
        if(t.Contains("firmware")&&(t.Contains("unsupported")||t.Contains("not supported"))) tips.Add("Firmware déclaré non pris en charge : comparez la version exacte du payload et sa documentation. Ne relancez pas un payload d'une autre version.");
        if(t.Contains("kstuff")&&(t.Contains("fail")||t.Contains("error")))tips.Add("Échec kstuff : vérifiez les instructions de la version etaHEN utilisée et la présence d'une instance déjà chargée. Ne superposez pas deux instances.");
        if(t.Contains("connection refused")||t.Contains("10061"))tips.Add("Connexion refusée : le service visé n'écoute probablement pas. Vérifiez IP, port et activation du FTP/loader.");
        if(t.Contains("550"))tips.Add("FTP 550 : chemin absent ou accès refusé. Vérifiez les permissions et le répertoire, puis essayez une opération en lecture.");
        if(t.Contains("timeout")||t.Contains("timed out"))tips.Add("Délai dépassé : vérifiez le réseau local, l'état de la console et les règles du pare-feu.");
        return tips.Count==0?"Aucune règle connue ne correspond. Conservez le journal et consultez la documentation de votre build. Ce diagnostic à règles ne confirme pas une cause matérielle.":string.Join("\n\n",tips);
    }
}
