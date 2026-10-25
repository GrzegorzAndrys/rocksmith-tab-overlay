using System.Windows;
using System.Windows.Interop;

namespace RocksmithTabOverlay;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Rocksmith renders through DirectX. Keeping the transparent overlay off
        // the GPU avoids layered-window/driver corruption during scene changes.
        System.Windows.Media.RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
