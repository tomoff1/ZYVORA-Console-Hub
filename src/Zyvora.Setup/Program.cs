using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Zyvora.Setup;
public static class Program
{
    const string UninstallKey=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\ZYVORA";
    static string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","ZYVORA");
    static string Link=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),"Programs","ZYVORA.lnk");
    static Stream Resource(string name)=>Assembly.GetExecutingAssembly().GetManifestResourceStream(name)??throw new IOException("Ressource absente.");
    static string SafePath(string root,string relative)
    {var full=Path.GetFullPath(Path.Combine(root,relative));if(Path.IsPathRooted(relative)||!full.StartsWith(Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))throw new IOException("Chemin interdit.");return full;}
    static void EnsureNoLinks(string path){var dir=new DirectoryInfo(path);while(dir!=null){if(dir.Exists&&(dir.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Installation dans un lien de répertoire refusée.");dir=dir.Parent;}}
    static Dictionary<string,string> Manifest(){using var s=Resource("app-manifest.json");return JsonSerializer.Deserialize<Dictionary<string,string>>(s)??throw new IOException("Manifeste invalide.");}
    static void Extract(string? target)
    {
        var manifest=Manifest();using var stream=Resource("app.zip");using var zip=new ZipArchive(stream,ZipArchiveMode.Read);
        var entries=zip.Entries.Where(e=>!string.IsNullOrEmpty(e.Name)).ToArray();if(entries.Length!=manifest.Count)throw new IOException("Archive inattendue.");
        if(target!=null){EnsureNoLinks(target);Directory.CreateDirectory(target);}
        foreach(var entry in entries){var relative=entry.FullName.Replace('/',Path.DirectorySeparatorChar);SafePath(target??Root,relative);if(!manifest.TryGetValue(entry.FullName.Replace('\\','/'),out var expected))throw new IOException("Fichier non répertorié.");using var input=entry.Open();var hash=Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();if(hash!=expected)throw new IOException("Intégrité incorrecte : "+entry.FullName);}
        if(target==null)return;
        foreach(var entry in entries){var output=SafePath(target,entry.FullName);Directory.CreateDirectory(Path.GetDirectoryName(output)!);entry.ExtractToFile(output,true);}
    }
    static void Install()
    {
        if(Process.GetProcessesByName("ZYVORA").Length>0)throw new IOException("Fermez ZYVORA avant l’installation.");
        EnsureNoLinks(Root);var version="0.1.0-"+Guid.NewGuid().ToString("N")[..8];var target=Path.Combine(Root,version);Extract(target);
        var uninstaller=Path.Combine(Root,"Uninstall.exe");File.Copy(Environment.ProcessPath!,uninstaller,true);
        var wsh=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;dynamic shell=wsh;dynamic shortcut=shell.CreateShortcut(Link);shortcut.TargetPath=Path.Combine(target,"ZYVORA.exe");shortcut.WorkingDirectory=target;shortcut.Description="ZYVORA — Console Hub";shortcut.Save();
        using var key=Registry.CurrentUser.CreateSubKey(UninstallKey);key.SetValue("DisplayName","ZYVORA — Console Hub");key.SetValue("Publisher","ZYVORA");key.SetValue("DisplayVersion","0.1.0");key.SetValue("InstallLocation",Root);key.SetValue("UninstallString","\""+uninstaller+"\" --uninstall");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);
        var existing=new List<string>();var receipt=Path.Combine(Root,"installed-files.json");if(File.Exists(receipt))existing=JsonSerializer.Deserialize<List<string>>(File.ReadAllText(receipt))??[];
        existing.AddRange(Manifest().Keys.Select(k=>version+"/"+k));existing.Add("Uninstall.exe");File.WriteAllText(receipt,JsonSerializer.Serialize(existing.Distinct().ToList()));
    }
    static void Remove()
    {
        EnsureNoLinks(Root);if(Process.GetProcessesByName("ZYVORA").Length>0)throw new IOException("Fermez ZYVORA avant la désinstallation.");
        var receipt=Path.Combine(Root,"installed-files.json");var files=JsonSerializer.Deserialize<List<string>>(File.ReadAllText(receipt))??throw new IOException("Reçu absent.");
        foreach(var relative in files){var path=SafePath(Root,relative);EnsureNoLinks(Path.GetDirectoryName(path)!);if(File.Exists(path))File.Delete(path);}
        File.Delete(receipt);if(File.Exists(Link))File.Delete(Link);Registry.CurrentUser.DeleteSubKeyTree(UninstallKey,false);
        foreach(var dir in Directory.GetDirectories(Root,"*",SearchOption.AllDirectories).OrderByDescending(d=>d.Length))if(!Directory.EnumerateFileSystemEntries(dir).Any())Directory.Delete(dir);
        if(!Directory.EnumerateFileSystemEntries(Root).Any())Directory.Delete(Root);
    }
    [STAThread] public static int Main(string[] args)
    {
        try{
            if(args.Contains("--verify-payload")){Extract(null);return 0;}
            if(args.Length==2&&args[0]=="--extract-test"){Extract(args[1]);return 0;}
            if(args.Contains("--remove")){Thread.Sleep(1800);Remove();MessageBox.Show("ZYVORA a été désinstallé. Les préférences locales sont conservées.","ZYVORA");return 0;}
            if(args.Contains("--uninstall")){if(MessageBox.Show("Désinstaller ZYVORA ?","ZYVORA",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return 0;var helper=Path.Combine(Path.GetTempPath(),"ZYVORA-uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Environment.ProcessPath!,helper);Process.Start(new ProcessStartInfo(helper,"--remove"){UseShellExecute=true});return 0;}
            var app=new Application();var window=new Window{Title="Installation de ZYVORA",Width=620,Height=450,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=new SolidColorBrush(Color.FromRgb(8,15,30)),Foreground=Brushes.White,FontFamily=new FontFamily("Segoe UI")};
            var panel=new StackPanel{Margin=new Thickness(32)};panel.Children.Add(new TextBlock{Text="Z Y V O R A",FontSize=32,FontWeight=FontWeights.Bold});panel.Children.Add(new TextBlock{Text="CONSOLE HUB / 0.1.0",Margin=new Thickness(0,8,0,22),Foreground=Brushes.LightBlue});panel.Children.Add(new TextBlock{Text="Installation gratuite pour votre compte Windows. Aucun droit administrateur nécessaire. Application autonome x64.\n\nConnexion FTP, transferts et payloads. Consultez STATUS.md pour les fonctions disponibles et leurs validations.",TextWrapping=TextWrapping.Wrap,FontSize=15});
            var terms=new CheckBox{Content="J’accepte la licence MIT et les limites documentées.",Margin=new Thickness(0,24,0,12)};panel.Children.Add(terms);var status=new TextBlock{Text="Destination : %LOCALAPPDATA%\\Programs\\ZYVORA",TextWrapping=TextWrapping.Wrap};panel.Children.Add(status);var install=new Button{Content="Installer ZYVORA",Padding=new Thickness(20,12,20,12),Margin=new Thickness(0,22,0,0),IsEnabled=false};terms.Checked+=(_,_)=>install.IsEnabled=true;terms.Unchecked+=(_,_)=>install.IsEnabled=false;panel.Children.Add(install);
            install.Click+=async(_,_)=>{if(install.Tag is true){window.Close();return;}install.IsEnabled=false;terms.IsEnabled=false;status.Text="Extraction et vérification des fichiers…";try{await Task.Run(Install);status.Text="Installation terminée. ZYVORA est disponible dans le menu Démarrer.";install.Content="Fermer";install.Tag=true;install.IsEnabled=true;}catch(Exception e){status.Text="Échec : "+e.Message;terms.IsEnabled=true;} };window.Content=panel;app.Run(window);return 0;
        }catch(Exception e){Console.Error.WriteLine(e.ToString());if(!args.Contains("--verify-payload")&&!(args.Length>0&&args[0]=="--extract-test"))MessageBox.Show(e.Message,"ZYVORA",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
    }
}
