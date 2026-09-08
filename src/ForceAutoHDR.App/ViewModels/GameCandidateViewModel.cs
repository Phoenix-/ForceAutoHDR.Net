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
    public GameCandidate Candidate { get; } = candidate;

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

    public Visibility RunningBadgeVisibility => IsRunning ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// When Game Bar last saw it, in the user's own date format. Undated entries say so rather
    /// than showing an epoch.
    /// </summary>
    public string LastPlayedText => IsRunning
        ? "Running now"
        : Candidate.LastPlayedUtc is { } utc
            ? utc.ToLocalTime().ToString("d")
            : "Never seen running";

    /// <summary>
    /// The automation name for the whole row; without it a screen reader reads the type name.
    /// </summary>
    public override string ToString() => $"{DisplayName}, {LastPlayedText}";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
