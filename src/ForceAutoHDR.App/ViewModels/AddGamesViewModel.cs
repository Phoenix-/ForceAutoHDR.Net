using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForceAutoHDR.Core.Discovery;
using Microsoft.UI.Xaml;

namespace ForceAutoHDR.App.ViewModels;

/// <summary>
/// State of the "add games" dialog: what discovery found, minus what is already configured.
/// </summary>
public sealed partial class AddGamesViewModel : INotifyPropertyChanged
{
    private readonly GameDiscoveryService _discovery;
    private readonly HashSet<string> _alreadyConfigured;
    private bool _isProbing;

    /// <param name="discovery">Source of candidates.</param>
    /// <param name="alreadyConfiguredPaths">
    /// Paths the main list already shows. Offering them again would invite the user to "add" a
    /// game that is sitting right behind the dialog.
    /// </param>
    public AddGamesViewModel(GameDiscoveryService discovery, IEnumerable<string> alreadyConfiguredPaths)
    {
        _discovery = discovery;
        _alreadyConfigured = new HashSet<string>(alreadyConfiguredPaths, StringComparer.OrdinalIgnoreCase);
        Reload();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when something went wrong; the dialog shows it in an InfoBar.</summary>
    public event Action<string>? Failed;

    public ObservableCollection<GameCandidateViewModel> Candidates { get; } = [];

    /// <summary>True while the GPU counters are being sampled, which takes about a second.</summary>
    public bool IsProbing
    {
        get => _isProbing;
        private set
        {
            if (_isProbing == value)
            {
                return;
            }

            _isProbing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(ProbeRingVisibility));
        }
    }

    /// <summary>Inverse of <see cref="IsProbing"/>, for disabling the buttons while it runs.</summary>
    public bool IsIdle => !IsProbing;

    public Visibility ProbeRingVisibility => IsProbing ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => Candidates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Whether anything is ticked. The dialog's Add button follows this, so confirming can never
    /// be a no-op that looks like it worked.
    /// </summary>
    public bool HasSelection
    {
        get
        {
            foreach (var candidate in Candidates)
            {
                if (candidate.IsSelected)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Paths of the ticked rows, in the order they are shown.</summary>
    public IReadOnlyList<string> SelectedPaths
    {
        get
        {
            var selected = new List<string>();
            foreach (var candidate in Candidates)
            {
                if (candidate.IsSelected)
                {
                    selected.Add(candidate.ExecutablePath);
                }
            }

            return selected;
        }
    }

    /// <summary>
    /// Samples the GPU counters and folds whatever is rendering into the list, ticked and on top.
    /// </summary>
    /// <remarks>
    /// This is the escape hatch for everything Game Bar never recorded, so a hit is worth
    /// selecting automatically: the user pressed the button while their game was running, and the
    /// answer is what they came for.
    /// </remarks>
    public Task DetectRunningAsync() => ProbeRunningAsync(announce: true, select: true);

    /// <summary>
    /// The same probe, run unprompted while the dialog opens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Discovery misses whole categories of game on its own. Game Bar records a game only when it
    /// matches a list Microsoft ships, so one installed outside the layout that list expects is
    /// never recorded at all, and one that moves keeps its old, dead path -- see
    /// <c>notes/gamebar-matches-games-against-a-microsoft-list.md</c>. Such a game is invisible
    /// here until something looks at what is actually rendering, and expecting the user to know
    /// that is how this got reported as "my game is missing" in the first place.
    /// </para>
    /// <para>
    /// Unlike the button, this ticks nothing and says nothing when it finds nothing: the user did
    /// not ask for it, so it may offer a row, but it must never pre-select one -- Enter confirms
    /// this dialog -- nor scold about an empty result.
    /// </para>
    /// </remarks>
    public Task AutoDetectRunningAsync() => ProbeRunningAsync(announce: false, select: false);

    private async Task ProbeRunningAsync(bool announce, bool select)
    {
        IsProbing = true;
        try
        {
            // The probe blocks for a second while it takes its two samples; off the UI thread it goes.
            var running = await Task.Run(() => new RunningGameProbe().GetCandidates());
            var found = 0;
            foreach (var candidate in running)
            {
                if (_alreadyConfigured.Contains(candidate.ExecutablePath))
                {
                    continue;
                }

                found++;
                Promote(candidate, select);
            }

            if (found == 0 && announce)
            {
                Failed?.Invoke(running.Count == 0
                    ? "Nothing is rendering right now. Start the game, let it reach a menu, then try again."
                    : "The game that is running is already configured.");
            }
        }
        catch (Exception ex)
        {
            // Reported even when unprompted: a counter query that throws is a real fault, and the
            // silent version of it is a dialog that looks like it simply forgot the running game.
            Failed?.Invoke($"Could not read the GPU counters: {ex.Message}");
        }
        finally
        {
            IsProbing = false;
        }
    }

    /// <summary>
    /// Adds a hand-picked executable as a ticked row, so browsing and discovery end up in the same
    /// confirmation step instead of being two different ways to add a game.
    /// </summary>
    public void AddManually(string executablePath)
    {
        if (_alreadyConfigured.Contains(executablePath))
        {
            Failed?.Invoke($"{Path.GetFileName(executablePath)} is already in the list.");
            return;
        }

        Promote(
            new GameCandidate
            {
                ExecutablePath = executablePath,
                Origins = GameCandidateOrigins.None,
                ExecutableExists = true,
            },
            select: true);
    }

    private void Reload()
    {
        Candidates.Clear();
        try
        {
            foreach (var candidate in _discovery.GetCandidates())
            {
                if (!_alreadyConfigured.Contains(candidate.ExecutablePath))
                {
                    Candidates.Add(Track(new GameCandidateViewModel(candidate)));
                }
            }
        }
        catch (Exception ex)
        {
            Failed?.Invoke($"Could not read the list of games: {ex.Message}");
        }

        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    /// <summary>
    /// Moves a candidate to the top. An executable already listed is reused rather than
    /// duplicated -- the running game is usually one Game Bar knows about too.
    /// </summary>
    /// <param name="select">
    /// Whether to tick it as well. True when the user asked for this row (the button, the file
    /// picker); false when it arrived unprompted, so that confirming the dialog never adds
    /// something nobody chose. An already-ticked row is never unticked by a later sighting.
    /// </param>
    private void Promote(GameCandidate candidate, bool select)
    {
        for (var i = 0; i < Candidates.Count; i++)
        {
            if (!Candidates[i].ExecutablePath.Equals(candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var existing = Candidates[i];
            if (candidate.Origins.HasFlag(GameCandidateOrigins.Running))
            {
                // Without this the badge never appears for a game Game Bar already knew about,
                // which is most of them -- and with the probe now running unprompted, the badge is
                // the only sign it found anything.
                existing.MarkRunning();
            }

            existing.IsSelected = existing.IsSelected || select;
            if (i > 0)
            {
                Candidates.Move(i, 0);
            }

            return;
        }

        Candidates.Insert(0, Track(new GameCandidateViewModel(candidate) { IsSelected = select }));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>
    /// Watches a row's checkbox so the Add button can follow the selection. Rows live exactly as
    /// long as the dialog does, so there is nothing to unsubscribe from.
    /// </summary>
    private GameCandidateViewModel Track(GameCandidateViewModel candidate)
    {
        candidate.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameCandidateViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        };

        return candidate;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
