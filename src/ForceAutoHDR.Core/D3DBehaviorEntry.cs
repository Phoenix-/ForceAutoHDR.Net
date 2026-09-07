namespace ForceAutoHDR.Core;

/// <summary>
/// One <c>D3DBehaviors</c> override: a subkey under <c>HKCU\Software\Microsoft\Direct3D</c>.
/// </summary>
/// <param name="SubKeyName">
/// Name of the subkey. Arbitrary -- Windows matches on <paramref name="ExecutableName"/>, not on
/// this -- but it is the handle needed to edit or delete the entry.
/// </param>
/// <param name="ExecutableName">
/// The <c>Name</c> value: a bare file name such as <c>Endfield.exe</c>. Note the consequence --
/// this override applies to <em>every</em> process with that file name, wherever it lives.
/// </param>
/// <param name="Flags">Every flag in <c>D3DBehaviors</c>, including ones this app does not understand.</param>
public sealed record D3DBehaviorEntry(string SubKeyName, string ExecutableName, FlagString Flags)
{
    /// <summary>True when both buffer-upgrade flags are set, i.e. the override is actually forcing Auto HDR.</summary>
    public bool IsAutoHdrForced { get; } = D3DBehaviorsStore.ReadIsForced(Flags);
}
