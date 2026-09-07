using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForceAutoHDR.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace ForceAutoHDR.App.ViewModels;

/// <summary>
/// The whole application state: the list of configured games and the writes behind its switches.
/// </summary>
public sealed partial class MainViewModel : INotifyPropertyChanged
{
    private readonly AutoHdrService _service;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();

    public MainViewModel(AutoHdrService service)
    {
        _service = service;
        Reload();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when a registry operation failed; the page shows it in an InfoBar.</summary>
    public event Action<string>? Failed;

    public ObservableCollection<ProfileViewModel> Profiles { get; } = [];

    public Visibility EmptyStateVisibility => Profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ListVisibility => Profiles.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Re-reads the registry and rebuilds every row.</summary>
    public void Reload()
    {
        Profiles.Clear();
        try
        {
            foreach (var profile in _service.GetProfiles())
            {
                Profiles.Add(new ProfileViewModel(this, profile));
            }
        }
        catch (Exception ex)
        {
            Report("Could not read the registry", ex);
        }

        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(ListVisibility));
    }

    /// <summary>
    /// Adds a game by path and turns its Auto HDR on -- adding a game only to leave it off would
    /// be a strange thing to ask for.
    /// </summary>
    public void AddGame(string executablePath)
    {
        try
        {
            _service.SetAutoHdr(executablePath, AutoHdrState.Enabled);
        }
        catch (Exception ex)
        {
            Report($"Could not add {Path.GetFileName(executablePath)}", ex);
            return;
        }

        Reload();
    }

    public bool TrySetAutoHdr(ProfileViewModel row, bool enabled)
    {
        if (row.Profile.ExecutablePath is not { } path)
        {
            Report("This entry has no executable path", null);
            return false;
        }

        try
        {
            _service.SetAutoHdr(path, enabled ? AutoHdrState.Enabled : AutoHdrState.Disabled);
            return true;
        }
        catch (Exception ex)
        {
            Report($"Could not change Auto HDR for {row.DisplayName}", ex);
            return false;
        }
    }

    public bool TrySetForced(ProfileViewModel row, bool forced)
    {
        try
        {
            _service.SetForced(row.Profile.ExecutableName, forced);
        }
        catch (Exception ex)
        {
            Report($"Could not change the override for {row.DisplayName}", ex);
            return false;
        }

        // One override covers every path sharing the file name, so sibling rows just changed too.
        // They are updated in place rather than by reloading: a full rebuild would throw away the
        // scroll position on every toggle.
        SyncSiblings(row, forced);

        // A row that exists only because of an override has nothing left to show once it is gone.
        if (!forced && !row.HasPath)
        {
            _dispatcher.TryEnqueue(() => RemoveRow(row));
        }

        return true;
    }

    /// <summary>Removes the game from both mechanisms, leaving it unconfigured.</summary>
    public void Remove(ProfileViewModel row)
    {
        try
        {
            if (row.Profile.ExecutablePath is { } path)
            {
                _service.Remove(path);
            }
            else if (row.Profile.D3DSubKeyName is { } subKey)
            {
                _service.D3DBehaviors.RemoveSubKey(subKey);
            }
        }
        catch (Exception ex)
        {
            Report($"Could not remove {row.DisplayName}", ex);
            return;
        }

        // Removing a game also drops its override, which other installs of the same executable
        // were relying on.
        SyncSiblings(row, forced: false);
        RemoveRow(row);
    }

    /// <summary>
    /// Snaps a switch back after a failed write. Queued rather than immediate: the toggle is still
    /// mid-flight when the setter runs, and a synchronous change notification is swallowed.
    /// </summary>
    public void PostRevert(ProfileViewModel row, string propertyName) =>
        _dispatcher.TryEnqueue(() => row.RaisePropertyChanged(propertyName));

    /// <summary>Mirrors an override change onto every other row with the same executable name.</summary>
    private void SyncSiblings(ProfileViewModel row, bool forced)
    {
        foreach (var other in Profiles)
        {
            if (!ReferenceEquals(other, row) &&
                other.Profile.ExecutableName.Equals(row.Profile.ExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                other.SyncForced(forced);
            }
        }
    }

    private void RemoveRow(ProfileViewModel row)
    {
        if (Profiles.Remove(row))
        {
            OnPropertyChanged(nameof(EmptyStateVisibility));
            OnPropertyChanged(nameof(ListVisibility));
        }
    }

    private void Report(string what, Exception? ex)
    {
        var detail = ex is null ? string.Empty : $": {ex.Message}";
        Failed?.Invoke(what + detail);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
