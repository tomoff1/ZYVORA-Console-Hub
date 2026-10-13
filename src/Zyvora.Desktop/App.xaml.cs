using System.Windows;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
namespace Zyvora.Desktop;
public partial class App : Application {
    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);
        if(e.Args.Length==2 && e.Args[0]=="--screenshot"){
            var window=new MainWindow();var content=(UIElement)window.Content;window.Content=null;
            var surface=new Border{Background=window.Background,Child=content};
            surface.Measure(new Size(1280,850));surface.Arrange(new Rect(0,0,1280,850));surface.UpdateLayout();
            var bitmap=new RenderTargetBitmap(1280,850,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(e.Args[1]))encoder.Save(file);Shutdown();return;
        }
        new MainWindow().Show();
    }
}
