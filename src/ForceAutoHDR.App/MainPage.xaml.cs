using ForceAutoHDR.App.ViewModels;
using ForceAutoHDR.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace ForceAutoHDR.App;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        // Before InitializeComponent: the one-time x:Bind bindings are evaluated inside it.
        ViewModel = new MainViewModel(AutoHdrService.ForCurrentUser());

        InitializeComponent();

        ViewModel.Failed += OnFailed;
    }

    public MainViewModel ViewModel { get; }

    private async void OnAddGameClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // Microsoft.Windows.Storage.Pickers, not the Windows.Storage one: this app is
            // unpackaged, and the WinRT picker needs an HWND bolted on by hand there.
            var picker = new FileOpenPicker(App.RootWindow!.AppWindow.Id)
            {
                Title = "Pick a game executable",
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                CommitButtonText = "Add",
            };
            picker.FileTypeFilter.Add(".exe");

            if (await picker.PickSingleFileAsync() is { } file)
            {
                ViewModel.AddGame(file.Path);
            }
        }
        catch (Exception ex)
        {
            OnFailed($"Could not open the file picker: {ex.Message}");
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        ErrorBar.IsOpen = false;
        ViewModel.Reload();
    }

    private void OnFailed(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }
}
