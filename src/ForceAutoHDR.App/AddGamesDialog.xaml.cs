using ForceAutoHDR.App.ViewModels;
using ForceAutoHDR.Core.Discovery;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace ForceAutoHDR.App;

/// <summary>
/// Picks games out of what discovery found, rather than asking the user to know where the
/// executable lives.
/// </summary>
public sealed partial class AddGamesDialog : ContentDialog
{
    /// <param name="alreadyConfiguredPaths">Paths the main list already shows.</param>
    public AddGamesDialog(IEnumerable<string> alreadyConfiguredPaths)
    {
        // Before InitializeComponent: the one-time x:Bind bindings are evaluated inside it.
        ViewModel = new AddGamesViewModel(GameDiscoveryService.ForCurrentUser(), alreadyConfiguredPaths);

        InitializeComponent();

        ViewModel.Failed += OnMessage;
    }

    public AddGamesViewModel ViewModel { get; }

    /// <summary>The executables the user ticked. Empty unless the dialog was confirmed.</summary>
    public IReadOnlyList<string> SelectedPaths { get; private set; } = [];

    private async void OnDetectRunningClick(object sender, RoutedEventArgs e)
    {
        MessageBar.IsOpen = false;
        await ViewModel.DetectRunningAsync();
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        MessageBar.IsOpen = false;
        try
        {
            // Microsoft.Windows.Storage.Pickers, not the Windows.Storage one: this app is
            // unpackaged, and the WinRT picker needs an HWND bolted on by hand there.
            var picker = new FileOpenPicker(App.RootWindow!.AppWindow.Id)
            {
                Title = "Pick a game executable",
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                CommitButtonText = "Select",
            };
            picker.FileTypeFilter.Add(".exe");

            if (await picker.PickSingleFileAsync() is { } file)
            {
                ViewModel.AddManually(file.Path);
            }
        }
        catch (Exception ex)
        {
            OnMessage($"Could not open the file picker: {ex.Message}");
        }
    }

    /// <summary>
    /// Captures the ticked rows before the dialog closes; afterwards the view model is gone and
    /// the caller has nothing left to read.
    /// </summary>
    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) =>
        SelectedPaths = ViewModel.SelectedPaths;

    private void OnMessage(string message)
    {
        MessageBar.Message = message;
        MessageBar.IsOpen = true;
    }
}
