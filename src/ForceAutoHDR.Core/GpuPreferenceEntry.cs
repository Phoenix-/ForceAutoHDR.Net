namespace ForceAutoHDR.Core;

/// <summary>
/// One per-app entry under <c>UserGpuPreferences</c> -- the same rows the Settings &gt; Display
/// &gt; Graphics page edits.
/// </summary>
/// <param name="ExecutablePath">Full path to the executable; this is the registry value's name.</param>
/// <param name="Flags">Every flag in the value, including the ones this app does not understand.</param>
public sealed record GpuPreferenceEntry(string ExecutablePath, FlagString Flags)
{
    /// <summary>The Auto HDR toggle decoded from <see cref="Flags"/>.</summary>
    public AutoHdrState AutoHdr { get; } = GpuPreferencesStore.ReadAutoHdrState(Flags);

    /// <summary>File name of the executable, e.g. <c>Wow.exe</c>.</summary>
    public string ExecutableName { get; } = Path.GetFileName(ExecutablePath);
}
