using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForceAutoHDR.Core.Discovery;
using Microsoft.UI.Xaml;

namespace ForceAutoHDR.App.ViewModels;

/// <summary>
/// One discovered game in the add dialog: a checkbox and enough context to judge it.
/// </summary>
/// <remarks>
/// Judging is the point. Discovery is deliberately permissive -- Game Bar's list is what Windows
/// saw rendering, which includes Electron apps -- so every row is a suggestion the user accepts or
/// ignores, never something applied on their behalf.
/// </remarks>
public sealed partial class GameCandidateViewModel(GameCandidate candidate) : INotifyPropertyChanged
{
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The candidate as discovery reported it.</summary>
    public GameCandidate Candidate { get; private set; } = candidate;

    public string DisplayName => Candidate.DisplayName;

    public string ExecutablePath => Candidate.ExecutablePath;

    /// <summary>Whether this row will be added when the dialog is confirmed.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Rendering right now, which is the strongest evidence this is a game at all.</summary>
    public bool IsRunning => Candidate.Origins.HasFlag(GameCandidateOrigins.Running);

    /// <summary>
    /// Records that the probe caught this executable rendering, on a row that already existed.
    /// </summary>
    /// <remarks>
    /// The common case, not an edge one: the running game is usually one Game Bar already knows,
    /// so the probe's answer arrives as a second sighting of a row that is on screen. The row is
    /// updated rather than replaced because replacing it would throw away the user's tick -- which
    /// means every value derived from the candidate has to be re-raised by hand, and the bindings
    /// for them have to be <c>OneWay</c>.
    /// </remarks>
    public void MarkRunning()
    {
        if (IsRunning)
        {
            return;
        }

        Candidate = Candidate with
        {
            Origins = Candidate.Origins | GameCandidateOrigins.Running,
            // It is rendering as we speak, whatever Game Bar last wrote down.
            LastPlayedUtc = DateTime.UtcNow,
        };

        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(RunningBadgeVisibility));
        OnPropertyChanged(nameof(LastPlayedText));
        OnPropertyChanged(nameof(LastPlayedVisibility));
    }

    public Visibility RunningBadgeVisibility => IsRunning ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// When Game Bar last saw it, in the user's own date format. Undated entries say so rather
    /// than showing an epoch.
    /// </summary>
    /// <remarks>
    /// Still says "Running now" for a running game even though the column is hidden then: this is
    /// what <see cref="ToString"/> reads out, and a row announced as just its name would lose the
    /// one fact the badge conveys to everyone else.
    /// </remarks>
    public string LastPlayedText => IsRunning
        ? "Running now"
        : Candidate.LastPlayedUtc is { } utc
            ? utc.ToLocalTime().ToString("d")
            : "Never seen running";

    /// <summary>
    /// Hides the date beside the Running badge, which otherwise says the same thing twice.
    /// </summary>
    public Visibility LastPlayedVisibility => IsRunning ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// The automation name for the whole row; without it a screen reader reads the type name.
    /// </summary>
    public override string ToString() => $"{DisplayName}, {LastPlayedText}";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
