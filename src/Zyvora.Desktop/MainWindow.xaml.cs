using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Zyvora.Core;

namespace Zyvora.Desktop;
public partial class MainWindow : Window
{
    CancellationTokenSource? operation;
    bool reducedMotion;
    readonly string settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ZYVORA","settings.json");
    public MainWindow()
    {
        InitializeComponent();CatalogGrid.ItemsSource=CatalogService.Load(Path.Combine(AppContext.BaseDirectory,"catalog","payloads.json"));
        try {if(File.Exists(settingsPath)){var p=JsonSerializer.Deserialize<ConsoleProfile>(File.ReadAllText(settingsPath));if(p!=null){HostBox.Text=p.Host;FtpPortBox.Text=p.FtpPort.ToString();ManagerPortBox.Text=p.ManagerPort.ToString();LoaderPortBox.Text=p.LoaderPort.ToString();FirmwareBox.Text=p.Firmware;}}}catch{StatusText.Text="Préférences illisibles : valeurs par défaut chargées.";}
        Loaded+=async(_,_)=> {if(!string.IsNullOrWhiteSpace(HostBox.Text))await Connect();};
        Closing+=(_,_)=>operation?.Cancel();
    }
    ConsoleProfile Profile() {NetworkTools.RequirePrivateHost(HostBox.Text.Trim());int Port(TextBox box){if(!int.TryParse(box.Text,out int n)||n<1||n>65535)throw new ArgumentException("Port invalide.");return n;}return new(HostBox.Text.Trim(),Port(FtpPortBox),Port(ManagerPortBox),Port(LoaderPortBox),FirmwareBox.Text.Trim(),UserBox.Text,PasswordBox.Password);}
    IProgress<TransferProgress> Transfer()=>new Progress<TransferProgress>(p=>{Progress.Value=p.Total>0?Math.Min(100,p.Bytes*100.0/p.Total):0;StatusText.Text=$"Transfert : {p.Bytes/1048576.0:F1} / {p.Total/1048576.0:F1} Mio";});
    async Task Run(string label,Func<CancellationToken,Task> action)
    {
        if(operation!=null){MessageBox.Show(this,"Une opération est déjà en cours.","ZYVORA");return;}
        operation=new CancellationTokenSource(TimeSpan.FromMinutes(30));StatusText.Text=label;Progress.Value=0;
        try {await action(operation.Token);Progress.Value=100;StatusText.Text="Terminé • "+label;}
        catch(OperationCanceledException){StatusText.Text="Opération annulée. Un envoi distant peut laisser un fichier partiel.";}
        catch(Exception e){StatusText.Text="Erreur • "+e.Message;MessageBox.Show(this,e.Message+"\n\n"+Diagnostics.Analyze(e.Message),"ZYVORA",MessageBoxButton.OK,MessageBoxImage.Warning);}
        finally{operation.Dispose();operation=null;}
    }
    async Task WithFtp(CancellationToken ct,Func<FtpSession,Task> action) {await using var ftp=await FtpSession.Open(Profile(),ct);await action(ftp);}
    async Task Refresh(CancellationToken ct) {await WithFtp(ct,async ftp=>FilesGrid.ItemsSource=await ftp.List(RemotePath.Text,ct));}
    async Task Connect()=>await Run("Connexion et lecture du FTP",async ct=>{ConnectionBadge.Text="●  Connexion…";await Refresh(ct);ConnectionBadge.Text="●  FTP connecté";try{using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(4000);var version=await NetworkTools.ManagerVersion(Profile(),timeout.Token);ConnectionBadge.Text="●  FTP + Manager "+version.Trim();}catch{ConnectionBadge.Text="●  FTP connecté • Manager indisponible";}SaveSettings();if(!reducedMotion){BeginAnimation(OpacityProperty,new DoubleAnimation(.4,1,TimeSpan.FromMilliseconds(350)));}});
    void SaveSettings(){var p=Profile();Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);File.WriteAllText(settingsPath,JsonSerializer.Serialize(p with{User="anonymous",Password=""}));}
    string Remote(string name)=>RemotePath.Text.TrimEnd('/')+"/"+name;
    RemoteEntry Selection()=>FilesGrid.SelectedItem as RemoteEntry??throw new InvalidOperationException("Sélectionnez un fichier ou dossier.");
    bool Confirm(string message)=>MessageBox.Show(this,message,"ZYVORA — Confirmation",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
    string? OpenFile(string filter){var d=new OpenFileDialog{Filter=filter};return d.ShowDialog(this)==true?d.FileName:null;}
    string? SaveFile(string name){var d=new SaveFileDialog{FileName=name};return d.ShowDialog(this)==true?d.FileName:null;}
    string? Folder(){var d=new OpenFolderDialog();return d.ShowDialog(this)==true?d.FolderName:null;}
    string? Prompt(string title,string initial="",bool multiline=false)
    {
        var dialog=new Window{Title=title,Width=multiline?760:450,Height=multiline?540:190,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Background};
        var grid=new Grid{Margin=new Thickness(18)};grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var text=new TextBox{Text=initial,AcceptsReturn=multiline,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};grid.Children.Add(text);var button=new Button{Content="Valider",HorizontalAlignment=HorizontalAlignment.Right};Grid.SetRow(button,1);grid.Children.Add(button);button.Click+=(_,_)=>{dialog.DialogResult=true;};dialog.Content=grid;return dialog.ShowDialog()==true?text.Text:null;
    }
    static void SafeName(string name){FtpSession.ValidatePath(name);if(name.Contains('/')||name.Contains('\\')||name is "." or "..")throw new ArgumentException("Nom invalide.");}
    async void Connect_Click(object s,RoutedEventArgs e)=>await Connect();
    async void Discover_Click(object s,RoutedEventArgs e)=>await Run("Recherche des services PS5 sur les sous-réseaux locaux",async ct=>{var found=await NetworkTools.Discover(ct);if(found.Count==0)throw new IOException("Aucun serveur FTP PS5 identifié. Saisissez l’IP et le port manuellement.");var p=found[0];if(found.Count>1){var selected=Prompt("Consoles détectées : "+string.Join(", ",found.Select(f=>f.Host)),p.Host);p=found.FirstOrDefault(f=>f.Host==selected)??throw new IOException("Choix de console invalide.");}HostBox.Text=p.Host;FtpPortBox.Text=p.FtpPort.ToString();await Refresh(ct);ConnectionBadge.Text="●  FTP identifié";SaveSettings();});
    async void Refresh_Click(object s,RoutedEventArgs e)=>await Run("Lecture du répertoire",Refresh);
    async void Parent_Click(object s,RoutedEventArgs e){var path=RemotePath.Text.TrimEnd('/');var index=path.LastIndexOf('/');RemotePath.Text=index<=0?"/":path[..index];await Run("Lecture du parent",Refresh);}
    async void Files_DoubleClick(object s,System.Windows.Input.MouseButtonEventArgs e){if(FilesGrid.SelectedItem is RemoteEntry{IsDirectory:true} row){RemotePath.Text=Remote(row.Name);await Run("Lecture du dossier",Refresh);}}
    async void Upload_Click(object s,RoutedEventArgs e){var d=new OpenFileDialog{Multiselect=true};if(d.ShowDialog(this)!=true)return;if(!Confirm("Envoyer les fichiers dans "+RemotePath.Text+" ? Les noms existants seront remplacés."))return;await Run("Envoi FTP",async ct=>{await WithFtp(ct,async ftp=>{foreach(var file in d.FileNames)await ftp.Upload(file,Remote(Path.GetFileName(file)),Transfer(),ct);});await Refresh(ct);});}
    async void Download_Click(object s,RoutedEventArgs e)=>await Run("Téléchargement FTP",async ct=>{var row=Selection();if(row.IsDirectory)throw new IOException("Utilisez Sauvegardes pour copier un dossier.");var target=SaveFile(row.Name);if(target!=null)await WithFtp(ct,ftp=>ftp.Download(Remote(row.Name),target,row.Size,Transfer(),ct));});
    async void Mkdir_Click(object s,RoutedEventArgs e)=>await Run("Création de dossier",async ct=>{var name=Prompt("Nom du dossier");if(name==null)return;SafeName(name);await WithFtp(ct,ftp=>ftp.MakeDirectory(Remote(name),ct));await Refresh(ct);});
    async void Rename_Click(object s,RoutedEventArgs e)=>await Run("Renommage",async ct=>{var row=Selection();var name=Prompt("Nouveau nom",row.Name);if(name==null)return;SafeName(name);await WithFtp(ct,ftp=>ftp.Rename(Remote(row.Name),Remote(name),ct));await Refresh(ct);});
    async void Delete_Click(object s,RoutedEventArgs e)=>await Run("Suppression",async ct=>{var row=Selection();if(!Confirm("Supprimer "+row.Name+" ? Cette suppression distante est définitive."))return;await WithFtp(ct,ftp=>ftp.Delete(Remote(row.Name),row.IsDirectory,ct));await Refresh(ct);});
    async void Edit_Click(object s,RoutedEventArgs e)=>await Run("Modification de texte",async ct=>{var row=Selection();if(row.IsDirectory||row.Size>1024*1024||!new[]{".txt",".ini",".json",".log",".cfg",".conf"}.Contains(Path.GetExtension(row.Name).ToLowerInvariant()))throw new IOException("Éditeur réservé aux fichiers texte connus de moins de 1 Mio.");var temp=Path.GetTempFileName();try{await WithFtp(ct,ftp=>ftp.Download(Remote(row.Name),temp,row.Size,null,ct));var edited=Prompt("Modifier "+row.Name,await File.ReadAllTextAsync(temp,ct),true);if(edited==null||!Confirm("Remplacer le fichier distant ? Faites une sauvegarde avant toute modification de configuration."))return;await File.WriteAllTextAsync(temp,edited,ct);await WithFtp(ct,ftp=>ftp.Upload(temp,Remote(row.Name),Transfer(),ct));}finally{File.Delete(temp);}await Refresh(ct);});
    async void Payload_Click(object s,RoutedEventArgs e){var file=OpenFile("Payload|*.elf;*.bin");if(file==null||!Confirm("Envoyer ce payload au loader TCP ? La compatibilité des fichiers locaux n'est pas certifiée. Cette opération peut exécuter du code sur la console."))return;await Run("Octets envoyés au loader — exécution non vérifiée",ct=>NetworkTools.SendPayload(Profile(),file,Transfer(),ct));}
    async void ManagerUpload_Click(object s,RoutedEventArgs e){var file=OpenFile("Payload|*.elf;*.bin");if(file==null||!Confirm("Stocker ce fichier dans Payload Manager ? Un fichier de même nom peut être remplacé."))return;await Run("Stockage confirmé par Payload Manager",ct=>NetworkTools.UploadToManager(Profile(),file,ct));}
    void CatalogInfo_Click(object s,RoutedEventArgs e){if(CatalogGrid.SelectedItem is CatalogItem i)MessageBox.Show(this,$"{i.Name}\n{i.Description}\n\nFirmware(s) déclarés : {string.Join(", ",i.Firmwares)}\nSource : {i.Source}\nSHA-256 : {i.Sha256}\nStatut : {i.Validation}\nValidation ZYVORA sur PS5 : non réalisée.","Provenance");}
    async void CatalogDownload_Click(object s,RoutedEventArgs e)=>await Run("Téléchargement vérifié",async ct=>{var item=CatalogGrid.SelectedItem as CatalogItem??throw new IOException("Sélectionnez une entrée.");var path=SaveFile(Path.GetFileName(new Uri(item.Url).AbsolutePath));if(path!=null)await CatalogService.Download(item,path,Transfer(),ct);});
    void Library_Click(object s,RoutedEventArgs e){var folder=Folder();if(folder==null)return;try{LibraryGrid.ItemsSource=new DirectoryInfo(folder).EnumerateFiles("*",SearchOption.TopDirectoryOnly).Where(f=>new[]{".pkg",".elf",".bin"}.Contains(f.Extension.ToLowerInvariant())).ToList();}catch(Exception ex){StatusText.Text=ex.Message;}}
    async void LibraryHash_Click(object s,RoutedEventArgs e)=>await Run("Vérification locale",async ct=>{var f=LibraryGrid.SelectedItem as FileInfo??throw new IOException("Sélectionnez un fichier.");MessageBox.Show(this,await CatalogService.Hash(f.FullName,ct),"SHA-256");});
    async void LibraryUpload_Click(object s,RoutedEventArgs e)=>await Run("Transfert bibliothèque (sans installation PKG)",async ct=>{var f=LibraryGrid.SelectedItem as FileInfo??throw new IOException("Sélectionnez un fichier.");if(!Confirm("Transférer "+f.Name+" dans "+RemotePath.Text+" ? Remplace le nom existant, sans installer le PKG."))return;await WithFtp(ct,ftp=>ftp.Upload(f.FullName,Remote(f.Name),Transfer(),ct));});
    async void Backup_Click(object s,RoutedEventArgs e){var folder=Folder();if(folder==null)return;await Run("Copie de sécurité et manifeste SHA-256",async ct=>await WithFtp(ct,ftp=>Backups.Create(ftp,BackupPath.Text,folder,Transfer(),ct)));}
    async void VerifyBackup_Click(object s,RoutedEventArgs e){var folder=Folder();if(folder==null)return;await Run("Vérification sauvegarde",async ct=>{var count=await Backups.Verify(folder,ct);MessageBox.Show(this,$"{count} fichiers vérifiés.","ZYVORA");});}
    void Analyze_Click(object s,RoutedEventArgs e)=>DiagnosticResult.Text=Diagnostics.Analyze(DiagnosticInput.Text);
    void ImportLog_Click(object s,RoutedEventArgs e){var path=OpenFile("Journaux|*.log;*.txt|Tous|*.*");if(path!=null){if(new FileInfo(path).Length>2*1024*1024){StatusText.Text="Journal trop volumineux (limite 2 Mio).";return;}DiagnosticInput.Text=File.ReadAllText(path);}}
    void Ocean_Click(object s,RoutedEventArgs e)=>Background=new SolidColorBrush(Color.FromRgb(8,15,30));
    void Purple_Click(object s,RoutedEventArgs e)=>Background=new SolidColorBrush(Color.FromRgb(25,12,40));
    void Motion_Click(object s,RoutedEventArgs e){reducedMotion=!reducedMotion;StatusText.Text=reducedMotion?"Animations réduites.":"Animations activées.";}
    void SaveSettings_Click(object s,RoutedEventArgs e){try{SaveSettings();StatusText.Text="Préférences enregistrées localement. Mot de passe non enregistré.";}catch(Exception ex){StatusText.Text=ex.Message;}}
    void Status_Click(object s,RoutedEventArgs e)=>Prompt("Statut des fonctions",File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"STATUS.md")),true);
    void Cancel_Click(object s,RoutedEventArgs e)=>operation?.Cancel();
}
