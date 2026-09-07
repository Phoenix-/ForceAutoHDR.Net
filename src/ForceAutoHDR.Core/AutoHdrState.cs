namespace ForceAutoHDR.Core;

/// <summary>State of the per-app Auto HDR toggle from Settings &gt; Display &gt; Graphics.</summary>
public enum AutoHdrState
{
    /// <summary>No <c>AutoHDREnable</c> flag: the app follows the system default.</summary>
    NotConfigured,

    /// <summary>Explicitly turned off (<c>AutoHDREnable=2096</c>).</summary>
    Disabled,

    /// <summary>Explicitly turned on (<c>AutoHDREnable=2097</c>).</summary>
    Enabled,
}
