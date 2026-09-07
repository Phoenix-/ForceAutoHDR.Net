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

    /// <summary>Executable name without its extension -- what to put in a list.</summary>
    public string DisplayName =>
        Path.GetFileNameWithoutExtension(ExecutableName) is { Length: > 0 } name ? name : ExecutableName;
}
