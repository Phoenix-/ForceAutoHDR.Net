using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForceAutoHDR.Core;
using ForceAutoHDR.Core.Discovery;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace ForceAutoHDR.App.ViewModels;

/// <summary>
/// The whole application state: the list of configured games and the writes behind its switches.
/// </summary>
public sealed partial class MainViewModel : INotifyPropertyChanged
{
    private readonly AutoHdrService _service;
    private readonly GameDiscoveryService _discovery;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private int _staleCount;

    public MainViewModel(AutoHdrService service, GameDiscoveryService discovery)
    {
        _service = service;
        _discovery = discovery;
        Reload();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when a registry operation failed; the page shows it in an InfoBar.</summary>
    public event Action<string>? Failed;

    public ObservableCollection<ProfileViewModel> Profiles { get; } = [];

    public Visibility EmptyStateVisibility => Profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ListVisibility => Profiles.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Configured rows whose executable is no longer on disk.</summary>
    public int StaleCount
    {
        get => _staleCount;
        private set
        {
            if (_staleCount == value)
            {
                return;
            }

            _staleCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StaleMessage));
            OnPropertyChanged(nameof(StaleBarVisibility));
        }
    }

    public Visibility StaleBarVisibility => StaleCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    public string StaleMessage => StaleCount == 1
        ? "One configured game is no longer installed."
        : $"{StaleCount} configured games are no longer installed.";

    /// <summary>Full paths of every configured row, for filtering the add dialog.</summary>
    public IReadOnlyList<string> ConfiguredPaths
    {
        get
        {
            var paths = new List<string>();
            foreach (var row in Profiles)
            {
                if (row.Profile.ExecutablePath is { } path)
                {
                    paths.Add(path);
                }
            }

            return paths;
        }
    }

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

        RefreshStaleCount();
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(ListVisibility));
    }

    /// <summary>
    /// Adds games with Auto HDR on -- adding a game only to leave it off would be a strange thing
    /// to ask for.
    /// </summary>
    /// <remarks>
    /// One failure does not abandon the rest: the games are unrelated, and silently dropping four
    /// of them because the first had a problem would be worse than reporting the one that failed.
    /// </remarks>
    public void AddGames(IReadOnlyList<string> executablePaths)
    {
        var failed = new List<string>();
        foreach (var path in executablePaths)
        {
            try
            {
                _service.SetAutoHdr(path, AutoHdrState.Enabled);
            }
            catch (Exception)
            {
                failed.Add(Path.GetFileName(path));
            }
        }

        Reload();

        if (failed.Count > 0)
        {
            Report($"Could not add {string.Join(", ", failed)}", null);
        }
    }

    /// <summary>
    /// Drops the per-app entries of games that are no longer installed.
    /// </summary>
    /// <remarks>
    /// Only the <c>UserGpuPreferences</c> row goes. Any forcing override is left alone on purpose:
    /// it is keyed by bare file name, so removing it would also disarm a second, still-installed
    /// copy of the same game -- which is exactly the situation two installs create.
    /// </remarks>
    public void CleanUpStale()
    {
        var removed = 0;
        try
        {
            foreach (var stale in _discovery.GetStalePreferences())
            {
                _service.GpuPreferences.Remove(stale.ExecutablePath);
                removed++;
            }
        }
        catch (Exception ex)
        {
            Report("Could not clean up every entry", ex);
        }

        if (removed > 0)
        {
            Reload();
        }
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
            RefreshStaleCount();
            OnPropertyChanged(nameof(EmptyStateVisibility));
            OnPropertyChanged(nameof(ListVisibility));
        }
    }

    /// <summary>
    /// Counts configured games that are gone from disk. Failure here is silent: the count drives a
    /// tidy-up hint, and nagging about a broken hint helps nobody.
    /// </summary>
    private void RefreshStaleCount()
    {
        try
        {
            StaleCount = _discovery.GetStalePreferences().Count;
        }
        catch (Exception)
        {
            StaleCount = 0;
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
