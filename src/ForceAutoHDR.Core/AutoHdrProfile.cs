using ForceAutoHDR.Core.Discovery;

namespace ForceAutoHDR.Core;

/// <summary>
/// One application as the UI should show it: the two independent mechanisms merged into a single
/// row.
/// </summary>
public sealed record AutoHdrProfile
{
    /// <summary>File name of the executable, e.g. <c>Wow.exe</c>. Always known.</summary>
    public required string ExecutableName { get; init; }

    /// <summary>
    /// Full path, or <see langword="null"/> for a profile known only through a
    /// <c>D3DBehaviors</c> override -- that mechanism stores a bare file name and nothing else.
    /// Without a path the per-app toggle cannot be set, since it is keyed by path.
    /// </summary>
    public string? ExecutablePath { get; init; }

    /// <summary>The per-app toggle from Settings &gt; Display &gt; Graphics.</summary>
    public AutoHdrState AutoHdr { get; init; }

    /// <summary>Whether a <c>D3DBehaviors</c> override forces the 10-bit swapchain upgrade.</summary>
    public bool IsForced { get; init; }

    /// <summary>Subkey backing <see cref="IsForced"/>, or <see langword="null"/> when there is no override.</summary>
    public string? D3DSubKeyName { get; init; }

    /// <summary>
    /// The game's name as a list should show it.
    /// </summary>
    /// <remarks>
    /// Derived from the whole path when there is one, so a row reads "Wuthering Waves" rather than
    /// "Client-Win64-Shipping" -- and, more to the point, so the same game is named identically
    /// here and in the discovery list it was added from. A profile known only through a
    /// <c>D3DBehaviors</c> override has just the file name to work with.
    /// </remarks>
    public string DisplayName => ExecutablePath is { } path
        ? GameName.FromPath(path)
        : Path.GetFileNameWithoutExtension(ExecutableName) is { Length: > 0 } name
            ? name
            : ExecutableName;
}
