using System.Net;
using System.Net.Sockets;
using System.Text;
using Zyvora.Core;

int passed=0;
void Check(bool condition,string label){if(!condition)throw new Exception("FAIL "+label);Console.WriteLine("PASS "+label);passed++;}
async Task Reject(Func<Task> action,string label){try{await action();}catch{Check(true,label);return;}throw new Exception("FAIL expected rejection: "+label);}
using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=deadline.Token;
var temp=Path.Combine(Path.GetTempPath(),"zyvora-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
try {
    if(args.Length>0&&args[0]=="--catalog") {var item=CatalogService.Load(args[1]).Single();var dest=Path.Combine(temp,"catalog.elf");await CatalogService.Download(item,dest,null,ct);Check(await CatalogService.Hash(dest,ct)==item.Sha256,"origin download matches published SHA-256");return;}
    if(args.Length>0 && args[0]=="--console") {
        var profile=new ConsoleProfile(args[1],int.Parse(args[2]));
        Check((await NetworkTools.ProbeFtp(profile.Host,profile.FtpPort,ct)).StartsWith("220"),"console FTP banner");
        await using var ftp=await FtpSession.Open(profile,ct);var rows=await ftp.List("/data",ct);Check(rows.Count>0,"console /data listing (names withheld)");
        Check((await NetworkTools.ManagerVersion(profile,ct)).Trim()=="0.5.2","console Payload Manager version");
        if(args.Contains("--transfer")){
            var dir="/data/zyvora-validation-"+Guid.NewGuid().ToString("N");var remote=dir+"/roundtrip.txt";bool made=false;
            try{await ftp.MakeDirectory(dir,ct);made=true;var local=Path.Combine(temp,"roundtrip.txt");await File.WriteAllTextAsync(local,"ZYVORA protocol verification — harmless temporary file\n",ct);await ftp.Upload(local,remote,null,ct);var back=Path.Combine(temp,"returned.txt");await ftp.Download(remote,back,new FileInfo(local).Length,null,ct);Check(await CatalogService.Hash(local,ct)==await CatalogService.Hash(back,ct),"console upload/download SHA-256 roundtrip");await ftp.Delete(remote,false,ct);}
            finally{if(made)await ftp.Delete(dir,true,ct);}
        }
        Console.WriteLine($"{passed} hardware checks passed");return;
    }
    Check(NetworkTools.IsPrivate(IPAddress.Parse("192.168.1.2"))&&!NetworkTools.IsPrivate(IPAddress.Parse("8.8.8.8")),"private network guard");
    await Reject(()=>Task.Run(()=>FtpSession.ValidatePath("/data/x\r\nDELE /data")),"FTP command injection blocked");
    await Reject(()=>Task.Run(()=>Backups.SafeDestination(temp,"../escape")),"backup traversal blocked");
    Check(FtpSession.ParseListing("type=dir; test\r\ntype=file;size=42; name with spaces.txt\r\ntype=pdir; ..\r\n",true).Count==2,"MLSD spaces and parent filtering");
    Check(FtpSession.ParseListing("-rw-r--r-- 1 root root 12 Oct 13 2026 hello world.txt\r\n",false).Single().Name=="hello world.txt","LIST spaces parser");
    var bytes=new byte[512*1024+37];new Random(9).NextBytes(bytes);var file=Path.Combine(temp,"sample.elf");await File.WriteAllBytesAsync(file,bytes,ct);
    foreach(bool fallback in new[]{false,true}) {
        await using var fixture=new FtpFixture(fallback);var p=new ConsoleProfile("127.0.0.1",fixture.Port);
        await using var ftp=await FtpSession.Open(p,ct);await ftp.MakeDirectory("/data/new",ct);await ftp.Upload(file,"/data/new/sample.elf",null,ct);
        Check((await ftp.List("/data/new",ct)).Single().Size==bytes.Length,$"FTP list fallback={fallback}");
        await ftp.Rename("/data/new/sample.elf","/data/new/renamed.elf",ct);var dest=Path.Combine(temp,"returned.elf");await ftp.Download("/data/new/renamed.elf",dest,bytes.Length,null,ct);
        Check((await File.ReadAllBytesAsync(dest,ct)).SequenceEqual(bytes),$"binary FTP roundtrip fallback={fallback}");
        var backup=Path.Combine(temp,"backup-"+fallback);await Backups.Create(ftp,"/data/new",backup,null,ct);Check(await Backups.Verify(backup,ct)==1,"backup hash verification");
        await File.AppendAllTextAsync(Path.Combine(backup,"renamed.elf"),"corrupt",ct);await Reject(()=>Backups.Verify(backup,ct),"backup corruption detected");
        await ftp.Delete("/data/new/renamed.elf",false,ct);await ftp.Delete("/data/new",true,ct);Check((await ftp.List("/data/new",ct)).Count==0,"FTP delete");
    }
    var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int loaderPort=((IPEndPoint)listener.LocalEndpoint).Port;
    var received=Task.Run(async()=>{using var tcp=await listener.AcceptTcpClientAsync(ct);using var result=new MemoryStream();await tcp.GetStream().CopyToAsync(result,ct);return result.ToArray();});
    await NetworkTools.SendPayload(new("127.0.0.1",LoaderPort:loaderPort),file,null,ct);Check((await received).SequenceEqual(bytes),"TCP payload exact bytes");listener.Stop();
    using var cancel=new CancellationTokenSource();cancel.Cancel();await Reject(()=>NetworkTools.SendPayload(new("127.0.0.1",LoaderPort:loaderPort),file,null,cancel.Token),"payload cancellation");
    var bad=new CatalogItem("x","x","1","http://invalid","","","",[],"unknown","");await Reject(()=>CatalogService.Download(bad,Path.Combine(temp,"bad"),null,ct),"catalog requires HTTPS licence checksum");
    Check(Diagnostics.Analyze("firmware not supported").Contains("Firmware"),"diagnostic firmware rule");
    Console.WriteLine($"{passed} tests passed");
} finally { Directory.Delete(temp,true); }

sealed class FtpFixture : IAsyncDisposable
{
    readonly TcpListener listener=new(IPAddress.Loopback,0);readonly CancellationTokenSource stop=new();readonly Task server;readonly bool fallback;
    readonly Dictionary<string,byte[]> files=new();readonly HashSet<string> dirs=["/data"];public int Port=>((IPEndPoint)listener.LocalEndpoint).Port;
    public FtpFixture(bool fallback){this.fallback=fallback;listener.Start();server=Serve();}
    async Task Serve(){try{while(!stop.IsCancellationRequested){using var client=await listener.AcceptTcpClientAsync(stop.Token);await Session(client);}}catch(OperationCanceledException){}catch(ObjectDisposedException){}}
    async Task Session(TcpClient client){using var r=new StreamReader(client.GetStream());using var w=new StreamWriter(client.GetStream(),new UTF8Encoding(false)){NewLine="\r\n",AutoFlush=true};TcpListener? data=null;string rename="";
        await w.WriteLineAsync("220-Test fixture\r\n220 PS5 fixture");string? line;
        try{while((line=await r.ReadLineAsync(stop.Token))!=null){int split=line.IndexOf(' ');string command=split<0?line:line[..split],path=split<0?"":line[(split+1)..];
            switch(command){
                case "USER":await w.WriteLineAsync("331 password");break;
                case "PASS":await w.WriteLineAsync("230 logged in");break;
                case "TYPE":await w.WriteLineAsync("200 binary");break;
                case "EPSV":case "PASV":if(command=="EPSV"&&fallback){await w.WriteLineAsync("500 unsupported");break;}data?.Stop();data=new(IPAddress.Loopback,0);data.Start();int p=((IPEndPoint)data.LocalEndpoint).Port;await w.WriteLineAsync(command=="EPSV"?$"229 Entering (|||{p}|)":$"227 Entering (1,2,3,4,{p/256},{p%256})");break;
                case "MLSD":case "LIST":if(command=="MLSD"&&fallback){await w.WriteLineAsync("500 unsupported");break;}await w.WriteLineAsync("150 listing");using(var tcp=await data!.AcceptTcpClientAsync(stop.Token)){using var listing=new StreamWriter(tcp.GetStream(),new UTF8Encoding(false)){NewLine="\r\n",AutoFlush=true};foreach(var f in files.Where(f=>Path.GetDirectoryName(f.Key)?.Replace('\\','/')==path)){var name=Path.GetFileName(f.Key);await listing.WriteLineAsync(command=="MLSD"?$"type=file;size={f.Value.Length}; {name}":$"-rw-r--r-- 1 root root {f.Value.Length} Oct 13 2026 {name}");}}data!.Stop();await w.WriteLineAsync("226 done");break;
                case "STOR":await w.WriteLineAsync("150 data");using(var tcp=await data!.AcceptTcpClientAsync(stop.Token)){using var memory=new MemoryStream();await tcp.GetStream().CopyToAsync(memory,stop.Token);files[path]=memory.ToArray();}data!.Stop();await w.WriteLineAsync("226 stored");break;
                case "RETR":if(!files.ContainsKey(path)){await w.WriteLineAsync("550 missing");break;}await w.WriteLineAsync("150 data");using(var tcp=await data!.AcceptTcpClientAsync(stop.Token)){await tcp.GetStream().WriteAsync(files[path],stop.Token);}data!.Stop();await w.WriteLineAsync("226 retrieved");break;
                case "MKD":dirs.Add(path);await w.WriteLineAsync("257 created");break;
                case "RNFR":rename=path;await w.WriteLineAsync("350 ready");break;
                case "RNTO":files[path]=files[rename];files.Remove(rename);await w.WriteLineAsync("250 renamed");break;
                case "DELE":files.Remove(path);await w.WriteLineAsync("250 deleted");break;
                case "RMD":dirs.Remove(path);await w.WriteLineAsync("250 removed");break;
                default:await w.WriteLineAsync("500 unknown");break;
            }
        }}finally{data?.Stop();}
    }
    public async ValueTask DisposeAsync(){stop.Cancel();listener.Stop();await server;stop.Dispose();}
}
