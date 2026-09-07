using Microsoft.UI.Xaml;

namespace ForceAutoHDR.App;

public partial class App : Application
{
    /// <summary>
    /// The one window. Pages reach it for things that need a window identity -- the file picker
    /// in an unpackaged app being the only one so far.
    /// </summary>
    public static MainWindow? RootWindow { get; private set; }

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        RootWindow = new MainWindow();
        RootWindow.Activate();
    }
}
