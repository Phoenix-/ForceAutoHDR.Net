using ForceAutoHDR.App.ViewModels;
using ForceAutoHDR.Core;
using ForceAutoHDR.Core.Discovery;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ForceAutoHDR.App;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        // Before InitializeComponent: the one-time x:Bind bindings are evaluated inside it.
        ViewModel = new MainViewModel(AutoHdrService.ForCurrentUser(), GameDiscoveryService.ForCurrentUser());

        InitializeComponent();

        ViewModel.Failed += OnFailed;
    }

    public MainViewModel ViewModel { get; }

    private async void OnAddGameClick(object sender, RoutedEventArgs e)
    {
        ErrorBar.IsOpen = false;
        try
        {
            var dialog = new AddGamesDialog(ViewModel.ConfiguredPaths)
            {
                // An unparented ContentDialog throws; the page's own root is the one to use.
                XamlRoot = XamlRoot,
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.SelectedPaths.Count > 0)
            {
                ViewModel.AddGames(dialog.SelectedPaths);
            }
        }
        catch (Exception ex)
        {
            OnFailed($"Could not open the game list: {ex.Message}");
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        ErrorBar.IsOpen = false;
        ViewModel.Reload();
    }

    private void OnCleanUpStaleClick(object sender, RoutedEventArgs e)
    {
        ErrorBar.IsOpen = false;
        ViewModel.CleanUpStale();
    }

    private void OnFailed(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }
}
