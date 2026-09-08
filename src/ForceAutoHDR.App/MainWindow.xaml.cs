using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace ForceAutoHDR.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SetIconFromExecutable();
        ShowBuildVersion();

        ResizeAndCenter(logicalWidth: 1020, logicalHeight: 700);

        RootFrame.Navigate(typeof(MainPage));
    }

    /// <summary>
    /// Sizes the window in logical units and centres it on the display it opened on.
    /// </summary>
    /// <remarks>
    /// <see cref="AppWindow"/> works in physical pixels, so a hard-coded size comes out small on a
    /// scaled display -- 125 % is the author's own setup. The DPI has to come from the HWND
    /// because there is no XamlRoot to ask before the first layout pass.
    /// </remarks>
    private void ResizeAndCenter(int logicalWidth, int logicalHeight)
    {
        var scale = GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        var width = (int)Math.Round(logicalWidth * scale);
        var height = (int)Math.Round(logicalHeight * scale);

        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        width = Math.Min(width, workArea.Width);
        height = Math.Min(height, workArea.Height);

        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + ((workArea.Width - width) / 2),
            workArea.Y + ((workArea.Height - height) / 2),
            width,
            height));
    }

    /// <summary>
    /// Takes the window icon from the one <c>&lt;ApplicationIcon&gt;</c> embedded in the exe.
    /// </summary>
    /// <remarks>
    /// The obvious <c>AppWindow.SetIcon("Assets/AppIcon.ico")</c> wants a loose file, and publish
    /// does not copy Content assets next to the exe -- that combination is what made the first
    /// published build die at startup with 0xC000027B. Reading the icon out of our own executable
    /// has nothing left to lose. See notes/winui3-publish-does-not-copy-content-assets.md.
    /// </remarks>
    private void SetIconFromExecutable()
    {
        if (Environment.ProcessPath is not { } executablePath)
        {
            return;
        }

        var icon = ExtractIcon(IntPtr.Zero, executablePath, 0);
        if (icon != IntPtr.Zero)
        {
            AppWindow.SetIcon(Win32Interop.GetIconIdFromIcon(icon));
        }
    }

    /// <summary>
    /// Puts the running build's version next to the name in the title bar.
    /// </summary>
    /// <remarks>
    /// Read out of the executable's own version resource rather than off
    /// <see cref="System.Reflection.AssemblyInformationalVersionAttribute"/>: assembly metadata is
    /// something the AOT compiler is free to drop, while the version resource is stamped into the
    /// exe by the SDK and is what File Explorer shows too. Anything that is not a release reads
    /// "0.1.0-dev+9b1c3f2", which is the point -- the one question about an exe someone was handed
    /// is which build it is, and it should not take a properties dialog to answer.
    /// </remarks>
    private void ShowBuildVersion()
    {
        if (Environment.ProcessPath is not { } executablePath)
        {
            return;
        }

        AppTitleBar.Subtitle = FileVersionInfo.GetVersionInfo(executablePath).ProductVersion ?? string.Empty;
    }

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(IntPtr hwnd);

    [LibraryImport("shell32.dll", EntryPoint = "ExtractIconW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr ExtractIcon(IntPtr hInst, string executablePath, int iconIndex);
}
