namespace ForceAutoHDR.Core.Discovery;

/// <summary>
/// One place candidate executables can be found. Sources are independent and deliberately
/// overlapping; <see cref="GameDiscoveryService"/> merges them.
/// </summary>
public interface IGameCandidateSource
{
    /// <summary>The origin this source stamps on what it returns.</summary>
    GameCandidateOrigins Origin { get; }

    /// <summary>
    /// Everything this source currently knows. Sources report what they see and do not filter on
    /// plausibility -- including entries whose executable is gone, flagged via
    /// <see cref="GameCandidate.ExecutableExists"/>.
    /// </summary>
    IReadOnlyList<GameCandidate> GetCandidates();
}
