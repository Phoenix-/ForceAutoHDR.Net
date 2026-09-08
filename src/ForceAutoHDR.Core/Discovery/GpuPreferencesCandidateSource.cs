using ForceAutoHDR.Core.Registry;

namespace ForceAutoHDR.Core.Discovery;

/// <summary>
/// The executables that already have a per-app graphics entry, surfaced as candidates so the
/// discovery list is a superset of what the app can already show.
/// </summary>
/// <remarks>
/// Its real value is the other direction: entries here whose file is gone are stale rows the user
/// can be offered a way to clean up. On the test machine 12 of 27 were exactly that.
/// </remarks>
public sealed class GpuPreferencesCandidateSource : IGameCandidateSource
{
    private readonly GpuPreferencesStore _store;
    private readonly Func<string, bool> _executableExists;

    /// <param name="registry">The hive to read.</param>
    /// <param name="executableExists">
    /// How to decide whether a path is still on disk; defaults to <see cref="File.Exists"/>.
    /// </param>
    public GpuPreferencesCandidateSource(IRegistryStore registry, Func<string, bool>? executableExists = null)
    {
        _store = new GpuPreferencesStore(registry);
        _executableExists = executableExists ?? File.Exists;
    }

    /// <inheritdoc />
    public GameCandidateOrigins Origin => GameCandidateOrigins.GpuPreferences;

    /// <inheritdoc />
    public IReadOnlyList<GameCandidate> GetCandidates()
    {
        var candidates = new List<GameCandidate>();
        foreach (var entry in _store.GetAll())
        {
            candidates.Add(new GameCandidate
            {
                ExecutablePath = entry.ExecutablePath,
                Origins = GameCandidateOrigins.GpuPreferences,
                // This key records a preference, never a launch: no timestamp exists to read.
                LastPlayedUtc = null,
                ExecutableExists = _executableExists(entry.ExecutablePath),
            });
        }

        return candidates;
    }
}
