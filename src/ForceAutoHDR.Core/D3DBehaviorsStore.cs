using ForceAutoHDR.Core.Registry;

namespace ForceAutoHDR.Core;

/// <summary>
/// The undocumented mechanism the original ForceAutoHDR (and ledoge/autohdr_force) uses: a subkey
/// per app under <c>HKCU\Software\Microsoft\Direct3D</c> that forces the D3D swapchain to be
/// upgraded to 10-bit, which is what makes Auto HDR engage for titles Windows will not offer it for.
/// </summary>
/// <remarks>
/// Independent of <see cref="GpuPreferencesStore"/>: this one is keyed by bare executable
/// <em>name</em>, so an override here hits every process with that file name.
/// </remarks>
public sealed class D3DBehaviorsStore(IRegistryStore registry)
{
    /// <summary>HKCU-relative path of the key holding one subkey per overridden app.</summary>
    public const string KeyPath = @"Software\Microsoft\Direct3D";

    public const string NameValueName = "Name";
    public const string BehaviorsValueName = "D3DBehaviors";
    public const string BufferUpgradeOverrideFlag = "BufferUpgradeOverride";
    public const string BufferUpgradeEnable10BitFlag = "BufferUpgradeEnable10Bit";

    private readonly IRegistryStore _registry = registry;

    /// <summary>
    /// Every override under the Direct3D key. Subkeys without both <c>Name</c> and
    /// <c>D3DBehaviors</c> are skipped -- that key has held unrelated Direct3D state for decades.
    /// </summary>
    public IReadOnlyList<D3DBehaviorEntry> GetAll()
    {
        var entries = new List<D3DBehaviorEntry>();
        foreach (var subKeyName in _registry.GetSubKeyNames(KeyPath))
        {
            var path = KeyPath + '\\' + subKeyName;
            var executableName = _registry.GetString(path, NameValueName);
            var behaviors = _registry.GetString(path, BehaviorsValueName);
            if (string.IsNullOrEmpty(executableName) || behaviors is null)
            {
                continue;
            }

            entries.Add(new D3DBehaviorEntry(subKeyName, executableName, FlagString.Parse(behaviors)));
        }

        return entries;
    }

    /// <summary>
    /// Every override matching an executable name. Usually zero or one, but nothing stops a user
    /// (or another tool) from creating several subkeys pointing at the same executable.
    /// </summary>
    public IReadOnlyList<D3DBehaviorEntry> Find(string executable)
    {
        var name = ToExecutableName(executable);
        return [.. GetAll().Where(e => e.ExecutableName.Equals(name, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// Adds or removes the buffer-upgrade override for an executable. Accepts either a bare file
    /// name or a full path -- only the file name is stored, because that is all Windows matches on.
    /// </summary>
    /// <remarks>
    /// Turning it off strips just the two buffer-upgrade flags and deletes the subkey once nothing
    /// is left, so an unrelated <c>D3DBehaviors</c> flag someone else put there survives.
    /// </remarks>
    public void SetForced(string executable, bool forced)
    {
        var name = ToExecutableName(executable);
        var existing = Find(name);

        if (forced)
        {
            var entry = existing.Count > 0 ? existing[0] : null;
            var subKeyName = entry?.SubKeyName ?? CreateSubKeyName(name);
            var flags = entry?.Flags ?? new FlagString(trailingSeparator: false);
            flags.Set(BufferUpgradeOverrideFlag, "1");
            flags.Set(BufferUpgradeEnable10BitFlag, "1");

            var path = KeyPath + '\\' + subKeyName;
            _registry.SetString(path, NameValueName, name);
            _registry.SetString(path, BehaviorsValueName, flags.ToString());
            return;
        }

        foreach (var entry in existing)
        {
            var path = KeyPath + '\\' + entry.SubKeyName;
            entry.Flags.Remove(BufferUpgradeOverrideFlag);
            entry.Flags.Remove(BufferUpgradeEnable10BitFlag);
            if (entry.Flags.IsEmpty)
            {
                _registry.DeleteKeyTree(path);
            }
            else
            {
                _registry.SetString(path, BehaviorsValueName, entry.Flags.ToString());
            }
        }
    }

    /// <summary>Deletes an override subkey outright, flags and all. A no-op when it is gone already.</summary>
    public void RemoveSubKey(string subKeyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(subKeyName);
        _registry.DeleteKeyTree(KeyPath + '\\' + subKeyName);
    }

    internal static bool ReadIsForced(FlagString flags) =>
        flags.TryGetInt32(BufferUpgradeOverrideFlag, out var over) && over != 0 &&
        flags.TryGetInt32(BufferUpgradeEnable10BitFlag, out var tenBit) && tenBit != 0;

    private static string ToExecutableName(string executable)
    {
        ArgumentException.ThrowIfNullOrEmpty(executable);

        var name = Path.GetFileName(executable);
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException($"'{executable}' does not name an executable.", nameof(executable));
        }

        return name;
    }

    /// <summary>
    /// Names the subkey after the executable, the way the original tool does, and keeps looking
    /// for a free name if something already sits there (a stale entry for a different app, say).
    /// </summary>
    private string CreateSubKeyName(string executableName)
    {
        var baseName = Path.GetFileNameWithoutExtension(executableName);
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = executableName;
        }

        var candidate = baseName;
        for (var suffix = 2; _registry.KeyExists(KeyPath + '\\' + candidate); suffix++)
        {
            candidate = $"{baseName}_{suffix}";
        }

        return candidate;
    }
}
