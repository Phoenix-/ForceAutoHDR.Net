using ForceAutoHDR.Core.Registry;

namespace ForceAutoHDR.Core.Discovery;

/// <summary>
/// Game Bar's own record of what it has seen render: one subkey per detected game under
/// <c>HKCU\System\GameConfigStore\Children</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the source that gets Auto HDR pointed at the right executable: the path here is the
/// process that owns the swapchain, which is routinely not the executable a store library names.
/// See <c>notes/gameconfigstore-is-the-real-game-list.md</c>.
/// </para>
/// <para>
/// It is not a live index, though. Game Bar fills it by matching processes against a list
/// Microsoft ships, so a game installed outside the layout that list expects never appears here at
/// all, and one that moves keeps its old, dead path forever. Neither case is recoverable from this
/// key -- that is what <see cref="RunningGameProbe"/> is for. See
/// <c>notes/gamebar-matches-games-against-a-microsoft-list.md</c>.
/// </para>
/// <para>
/// Read-only, and it will stay that way: the key belongs to Game Bar, and nothing good comes of a
/// third-party tool editing another component's private store.
/// </para>
/// </remarks>
public sealed class GameConfigStoreSource : IGameCandidateSource
{
    /// <summary>HKCU-relative path of the key holding one subkey per detected game.</summary>
    public const string KeyPath = @"System\GameConfigStore\Children";

    /// <summary>Full path of the executable Game Bar matched. The only value that makes an entry real.</summary>
    public const string MatchedExecutableValueName = "MatchedExeFullPath";

    /// <summary>REG_QWORD holding a UTC FILETIME of the last launch.</summary>
    public const string LastAccessedValueName = "LastAccessed";

    /// <summary>Microsoft's per-title id.</summary>
    public const string TitleIdValueName = "TitleId";

    private readonly IRegistryStore _registry;
    private readonly Func<string, bool> _executableExists;

    /// <param name="registry">The hive to read.</param>
    /// <param name="executableExists">
    /// How to decide whether a path is still on disk; defaults to <see cref="File.Exists"/>.
    /// Injectable because roughly half the entries on a real machine point at uninstalled games,
    /// which makes this the single most important thing to be able to fake in a test.
    /// </param>
    public GameConfigStoreSource(IRegistryStore registry, Func<string, bool>? executableExists = null)
    {
        _registry = registry;
        _executableExists = executableExists ?? File.Exists;
    }

    /// <inheritdoc />
    public GameCandidateOrigins Origin => GameCandidateOrigins.GameConfigStore;

    /// <summary>
    /// Every child that names an executable, newest first. Children without
    /// <see cref="MatchedExecutableValueName"/> are skipped -- on a real machine most of them have
    /// none, carrying only a title id for a game that was never installed here.
    /// </summary>
    /// <remarks>
    /// An empty result means "Game Bar told us nothing", which is also what a machine with Game Bar
    /// switched off looks like. It never means "this user has no games".
    /// </remarks>
    public IReadOnlyList<GameCandidate> GetCandidates()
    {
        // Keyed by path: the same executable can hold more than one subkey, and the freshest launch
        // is the interesting one.
        var byPath = new Dictionary<string, GameCandidate>(StringComparer.OrdinalIgnoreCase);

        foreach (var subKeyName in _registry.GetSubKeyNames(KeyPath))
        {
            var childPath = $@"{KeyPath}\{subKeyName}";
            var executablePath = _registry.GetString(childPath, MatchedExecutableValueName);
            if (string.IsNullOrEmpty(executablePath) || !Path.IsPathFullyQualified(executablePath))
            {
                continue;
            }

            var candidate = new GameCandidate
            {
                ExecutablePath = executablePath,
                Origins = GameCandidateOrigins.GameConfigStore,
                LastPlayedUtc = ReadLastAccessed(childPath),
                ExecutableExists = _executableExists(executablePath),
                TitleId = _registry.GetString(childPath, TitleIdValueName),
            };

            if (!byPath.TryGetValue(executablePath, out var existing) || IsNewer(candidate, existing))
            {
                byPath[executablePath] = candidate;
            }
        }

        var candidates = byPath.Values.ToList();
        candidates.Sort(static (a, b) =>
        {
            // Newest first; entries with no timestamp sink to the bottom rather than pretending to
            // be ancient, then sort by name so the order is at least stable.
            var byTime = Nullable.Compare(b.LastPlayedUtc, a.LastPlayedUtc);
            return byTime != 0
                ? byTime
                : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        return candidates;
    }

    private static bool IsNewer(GameCandidate candidate, GameCandidate existing) =>
        Nullable.Compare(candidate.LastPlayedUtc, existing.LastPlayedUtc) > 0;

    /// <summary>
    /// Reads <c>LastAccessed</c> as a UTC FILETIME. Out-of-range values are reported as unknown --
    /// this is another component's private store, and a garbled timestamp is not worth an
    /// exception on a list that is only ever advisory.
    /// </summary>
    private DateTime? ReadLastAccessed(string childPath)
    {
        if (_registry.GetInt64(childPath, LastAccessedValueName) is not { } fileTime || fileTime <= 0)
        {
            return null;
        }

        try
        {
            return DateTime.FromFileTimeUtc(fileTime);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
