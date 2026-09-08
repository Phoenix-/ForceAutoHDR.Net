namespace ForceAutoHDR.Core.Discovery;

/// <summary>
/// Where a candidate executable was found. A flags enum because the same executable usually turns
/// up in more than one place, and the combination is what the UI sorts and explains by.
/// </summary>
[Flags]
public enum GameCandidateOrigins
{
    /// <summary>Nowhere -- the empty set.</summary>
    None = 0,

    /// <summary>
    /// Game Bar's list of executables Windows observed rendering
    /// (<c>HKCU\System\GameConfigStore\Children</c>). The history: broad, offline, and naming the
    /// executable that actually owns the swapchain.
    /// </summary>
    GameConfigStore = 1,

    /// <summary>
    /// Already carries a per-app entry under <c>UserGpuPreferences</c>, i.e. it is configured --
    /// possibly by Windows, possibly by this app.
    /// </summary>
    GpuPreferences = 2,

    /// <summary>
    /// Rendering right now, per the GPU engine performance counters. The authority when Game Bar
    /// missed a game, at the cost of needing the game to be running.
    /// </summary>
    Running = 4,
}
