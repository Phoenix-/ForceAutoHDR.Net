namespace ForceAutoHDR.Core.Discovery;

/// <summary>
/// An executable that plausibly wants Auto HDR, before the user has said anything about it.
/// </summary>
/// <remarks>
/// A candidate is a suggestion, not a verdict: discovery is deliberately permissive, and a
/// non-game does slip through (Electron apps register with Game Bar). Deciding is the UI's job.
/// </remarks>
public sealed record GameCandidate
{
    /// <summary>Full path to the executable that owns the swapchain.</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>Every source that reported this executable.</summary>
    public required GameCandidateOrigins Origins { get; init; }

    /// <summary>
    /// When Game Bar last saw it run, or <see langword="null"/> when no source knew. Sorting the
    /// list by this puts what the user actually plays on top.
    /// </summary>
    public DateTime? LastPlayedUtc { get; init; }

    /// <summary>
    /// Whether the file is still on disk. Game Bar never prunes its list, so a good half of it can
    /// be uninstalled games.
    /// </summary>
    public bool ExecutableExists { get; init; }

    /// <summary>
    /// Microsoft's title id from Game Bar, when it came from there. Stable across installs, so it
    /// survives a game moving between drives; kept for matching, not shown.
    /// </summary>
    public string? TitleId { get; init; }

    /// <summary>File name of the executable, e.g. <c>Client-Win64-Shipping.exe</c>.</summary>
    public string ExecutableName => Path.GetFileName(ExecutablePath);

    /// <summary>Best guess at the game's name -- see <see cref="GameName"/> for how good that is.</summary>
    public string DisplayName => GameName.FromPath(ExecutablePath);
}
