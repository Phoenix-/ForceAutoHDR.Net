using ForceAutoHDR.Core.Registry;

namespace ForceAutoHDR.Core.Discovery;

/// <summary>
/// Merges every candidate source into the one list the UI offers: "these are the games we found,
/// which of them do you want Auto HDR on?"
/// </summary>
/// <remarks>
/// Store libraries are deliberately not a source. Steam and Epic name the executable they launch,
/// which for a launcher-fronted game is not the one that renders -- Auto HDR set on that path is
/// written happily and does nothing. Store metadata belongs on top of a discovered path (a nicer
/// title, an icon), never in place of it.
/// </remarks>
public sealed class GameDiscoveryService
{
    private readonly IReadOnlyList<IGameCandidateSource> _sources;

    /// <param name="sources">Queried in order; a source that returns nothing is simply skipped.</param>
    public GameDiscoveryService(params IGameCandidateSource[] sources) => _sources = sources;

    /// <summary>
    /// Binds to the live registry of the current user.
    /// </summary>
    /// <param name="includeRunningGames">
    /// Adds <see cref="RunningGameProbe"/>, which blocks for about a second while it samples the
    /// GPU counters. Off by default so building the list stays instant; turn it on for the
    /// "detect the running game" action.
    /// </param>
    public static GameDiscoveryService ForCurrentUser(bool includeRunningGames = false)
    {
        var registry = CurrentUserRegistryStore.Instance;
        return includeRunningGames
            ? new GameDiscoveryService(
                new GameConfigStoreSource(registry),
                new GpuPreferencesCandidateSource(registry),
                new RunningGameProbe())
            : new GameDiscoveryService(
                new GameConfigStoreSource(registry),
                new GpuPreferencesCandidateSource(registry));
    }

    /// <summary>
    /// Every candidate, deduplicated by path and sorted with what was played most recently first.
    /// </summary>
    /// <param name="includeMissingExecutables">
    /// Keeps candidates whose file is gone. Off by default: Game Bar never prunes its list, and on
    /// a real machine roughly half of it is uninstalled games. See
    /// <see cref="GetStalePreferences"/> for the one case where they matter.
    /// </param>
    public IReadOnlyList<GameCandidate> GetCandidates(bool includeMissingExecutables = false)
    {
        var byPath = new Dictionary<string, GameCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in _sources)
        {
            foreach (var candidate in source.GetCandidates())
            {
                byPath[candidate.ExecutablePath] =
                    byPath.TryGetValue(candidate.ExecutablePath, out var existing)
                        ? Merge(existing, candidate)
                        : candidate;
            }
        }

        var candidates = new List<GameCandidate>(byPath.Count);
        foreach (var candidate in byPath.Values)
        {
            if (includeMissingExecutables || candidate.ExecutableExists)
            {
                candidates.Add(candidate);
            }
        }

        candidates.Sort(static (a, b) =>
        {
            // A running game outranks everything, however long ago Game Bar last saw it.
            var byRunning = b.Origins.HasFlag(GameCandidateOrigins.Running)
                .CompareTo(a.Origins.HasFlag(GameCandidateOrigins.Running));
            if (byRunning != 0)
            {
                return byRunning;
            }

            // Most recent first; unknown timestamps sink rather than pose as ancient.
            var byLastPlayed = Nullable.Compare(b.LastPlayedUtc, a.LastPlayedUtc);
            return byLastPlayed != 0
                ? byLastPlayed
                : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        return candidates;
    }

    /// <summary>
    /// Configured applications whose executable is gone -- rows in <c>UserGpuPreferences</c> left
    /// behind by uninstalled games, which the user can be offered a way to clean up.
    /// </summary>
    /// <remarks>
    /// Only entries this app can act on are returned: a missing executable known solely to Game
    /// Bar is that component's business, not ours.
    /// </remarks>
    public IReadOnlyList<GameCandidate> GetStalePreferences()
    {
        var stale = new List<GameCandidate>();
        foreach (var candidate in GetCandidates(includeMissingExecutables: true))
        {
            if (!candidate.ExecutableExists && candidate.Origins.HasFlag(GameCandidateOrigins.GpuPreferences))
            {
                stale.Add(candidate);
            }
        }

        return stale;
    }

    /// <summary>
    /// Folds a second sighting of the same executable into the first: the origins accumulate, and
    /// the most informative value of each field wins.
    /// </summary>
    private static GameCandidate Merge(GameCandidate existing, GameCandidate addition) =>
        existing with
        {
            Origins = existing.Origins | addition.Origins,
            LastPlayedUtc = Nullable.Compare(addition.LastPlayedUtc, existing.LastPlayedUtc) > 0
                ? addition.LastPlayedUtc
                : existing.LastPlayedUtc,
            // Disagreement means one source raced the file system; "it is there" is the answer that
            // keeps a real game in the list.
            ExecutableExists = existing.ExecutableExists || addition.ExecutableExists,
            TitleId = existing.TitleId ?? addition.TitleId,
        };
}
