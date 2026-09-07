using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForceAutoHDR.Core;

namespace ForceAutoHDR.App.ViewModels;

/// <summary>
/// One row in the list: a game and its two switches.
/// </summary>
/// <remarks>
/// The setters write to the registry immediately -- there is no Apply button, because both
/// mechanisms take effect the next time the game starts and nothing is transactional anyway.
/// A failed write reverts the switch instead of leaving the UI lying about the registry.
/// </remarks>
public sealed partial class ProfileViewModel(MainViewModel owner, AutoHdrProfile profile) : INotifyPropertyChanged
{
    private bool _isAutoHdrEnabled = profile.AutoHdr == AutoHdrState.Enabled;
    private bool _isForced = profile.IsForced;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The profile as it was read from the registry.</summary>
    public AutoHdrProfile Profile { get; } = profile;

    public string DisplayName => Profile.DisplayName;

    /// <summary>
    /// The full path, or an explanation when there is none -- an entry that exists only as a
    /// D3DBehaviors override knows the file name and nothing more.
    /// </summary>
    public string PathText => Profile.ExecutablePath ?? $"{Profile.ExecutableName} — matched by file name, wherever it lives";

    /// <summary>
    /// The per-app toggle is keyed by full path, so an override-only row cannot have one.
    /// </summary>
    public bool HasPath => Profile.ExecutablePath is not null;

    /// <summary>Tooltip spelling out the difference between "off" and "not configured".</summary>
    public string AutoHdrTooltip => HasPath
        ? "The per-app switch from Settings > Display > Graphics. Off writes an explicit 'disabled'; use Remove to leave the game unconfigured."
        : "Needs a full executable path. This row comes from a D3DBehaviors override, which stores only a file name.";

    public bool IsAutoHdrEnabled
    {
        get => _isAutoHdrEnabled;
        set
        {
            if (_isAutoHdrEnabled == value)
            {
                return;
            }

            if (owner.TrySetAutoHdr(this, value))
            {
                _isAutoHdrEnabled = value;
                OnPropertyChanged();
            }
            else
            {
                // The switch has already moved on screen; snap it back once the toggle finishes.
                owner.PostRevert(this, nameof(IsAutoHdrEnabled));
            }
        }
    }

    public bool IsForced
    {
        get => _isForced;
        set
        {
            if (_isForced == value)
            {
                return;
            }

            if (owner.TrySetForced(this, value))
            {
                _isForced = value;
                OnPropertyChanged();
            }
            else
            {
                owner.PostRevert(this, nameof(IsForced));
            }
        }
    }

    /// <summary>Bound straight to the row's button via <c>x:Bind</c>, so no event plumbing.</summary>
    public void Remove() => owner.Remove(this);

    /// <summary>
    /// The ListView item's automation name falls back to this, so without it a screen reader
    /// announces "ForceAutoHDR.App.ViewModels.ProfileViewModel" for every row.
    /// </summary>
    public override string ToString() => DisplayName;

    internal void RaisePropertyChanged(string propertyName) => OnPropertyChanged(propertyName);

    /// <summary>
    /// Updates the switch to match the registry without writing anything back -- used when a
    /// *different* row's override changed this one too, which happens whenever two installed
    /// copies share an executable name.
    /// </summary>
    internal void SyncForced(bool forced)
    {
        if (_isForced == forced)
        {
            return;
        }

        _isForced = forced;
        OnPropertyChanged(nameof(IsForced));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
