using System.Text.Json;
namespace Zyvora.Core;
public record BackupFile(string Path,long Size,string Sha256);
public static class Backups
{
    public static string SafeDestination(string root,string relative)
    {
        if(System.IO.Path.IsPathRooted(relative))throw new InvalidDataException("Chemin absolu interdit.");
        var target=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative));
        if(!target.StartsWith(System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Chemin hors sauvegarde.");
        return target;
    }
    public static async Task Create(FtpSession ftp,string remote,string local,IProgress<TransferProgress>? progress,CancellationToken ct)
    {
        if(Directory.Exists(local)&&Directory.EnumerateFileSystemEntries(local).Any())throw new IOException("Choisissez un dossier vide.");
        Directory.CreateDirectory(local);var files=new List<BackupFile>();
        async Task Walk(string path,string relative,int depth) {
            if(depth>32||files.Count>100000)throw new IOException("Limite de sauvegarde atteinte.");
            foreach(var e in await ftp.List(path,ct)) { ct.ThrowIfCancellationRequested();if(relative==""&&e.Name=="zyvora-backup.json")throw new IOException("Nom réservé au manifeste : zyvora-backup.json.");var rel=System.IO.Path.Combine(relative,e.Name);var target=SafeDestination(local,rel);var next=path.TrimEnd('/')+"/"+e.Name;
                if(e.IsDirectory){Directory.CreateDirectory(target);await Walk(next,rel,depth+1);}else {Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);await ftp.Download(next,target,e.Size,progress,ct);files.Add(new(rel,new FileInfo(target).Length,await CatalogService.Hash(target,ct)));}
            }
        }
        await Walk(remote,"",0);await File.WriteAllTextAsync(System.IO.Path.Combine(local,"zyvora-backup.json"),JsonSerializer.Serialize(files,new JsonSerializerOptions{WriteIndented=true}),ct);
    }
    public static async Task<int> Verify(string folder,CancellationToken ct)
    {
        var entries=JsonSerializer.Deserialize<List<BackupFile>>(await File.ReadAllTextAsync(System.IO.Path.Combine(folder,"zyvora-backup.json"),ct))??throw new InvalidDataException("Manifeste invalide.");
        foreach(var e in entries) {var target=SafeDestination(folder,e.Path);if(new FileInfo(target).Length!=e.Size || await CatalogService.Hash(target,ct)!=e.Sha256)throw new InvalidDataException("Copie invalide : "+e.Path);}
        return entries.Count;
    }
}
