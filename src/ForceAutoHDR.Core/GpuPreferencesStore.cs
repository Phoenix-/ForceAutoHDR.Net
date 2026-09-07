using ForceAutoHDR.Core.Registry;

namespace ForceAutoHDR.Core;

/// <summary>
/// The documented, first-party mechanism: the per-app graphics preferences that Settings &gt;
/// Display &gt; Graphics writes. One REG_SZ per executable, named after its full path, holding a
/// <see cref="FlagString"/> in which <c>AutoHDREnable</c> is one flag among several.
/// </summary>
public sealed class GpuPreferencesStore(IRegistryStore registry)
{
    /// <summary>HKCU-relative path of the key holding all per-app preferences.</summary>
    public const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";

    /// <summary>
    /// The system-wide defaults entry. It is not the only non-application value in
    /// <see cref="KeyPath"/> -- see <see cref="IsApplicationValue"/>.
    /// </summary>
    public const string GlobalSettingsValueName = "DirectXUserGlobalSettings";

    /// <summary>Name of the Auto HDR flag inside an entry's <see cref="FlagString"/>.</summary>
    public const string AutoHdrFlag = "AutoHDREnable";

    // Observed on Windows 11 26200: 2097 for on, 2096 for off. Only bit 0 carries the on/off
    // state (the global entry uses a plain 1/0), so reading tolerates other encodings while
    // writing sticks to the exact values Windows itself produces.
    private const string AutoHdrEnabledValue = "2097";
    private const string AutoHdrDisabledValue = "2096";

    private readonly IRegistryStore _registry = registry;

    /// <summary>
    /// Every per-app entry, in registry order. Windows' own housekeeping values in the same key
    /// are excluded.
    /// </summary>
    public IReadOnlyList<GpuPreferenceEntry> GetAll()
    {
        var entries = new List<GpuPreferenceEntry>();
        foreach (var valueName in _registry.GetValueNames(KeyPath))
        {
            if (!IsApplicationValue(valueName))
            {
                continue;
            }

            entries.Add(new GpuPreferenceEntry(valueName, FlagString.Parse(_registry.GetString(KeyPath, valueName))));
        }

        return entries;
    }

    /// <summary>The entry for an executable, or <see langword="null"/> when it has none.</summary>
    public GpuPreferenceEntry? Find(string executablePath)
    {
        ValidateExecutablePath(executablePath);

        var storedName = FindValueName(executablePath);
        return storedName is null
            ? null
            : new GpuPreferenceEntry(storedName, FlagString.Parse(_registry.GetString(KeyPath, storedName)));
    }

    /// <summary>
    /// Turns the per-app Auto HDR toggle on, off, or back to "not configured", leaving every other
    /// flag Windows stored for that app untouched. Setting <see cref="AutoHdrState.NotConfigured"/>
    /// on an entry that has no other flags deletes the value outright, which is what the Settings
    /// page does too.
    /// </summary>
    public void SetAutoHdr(string executablePath, AutoHdrState state)
    {
        ValidateExecutablePath(executablePath);

        // Reuse the stored name so a path that differs only in casing does not create a twin.
        var valueName = FindValueName(executablePath) ?? executablePath;
        var flags = FlagString.Parse(_registry.GetString(KeyPath, valueName));

        switch (state)
        {
            case AutoHdrState.NotConfigured:
                if (!flags.Remove(AutoHdrFlag))
                {
                    return;
                }

                break;
            case AutoHdrState.Enabled:
                flags.Set(AutoHdrFlag, AutoHdrEnabledValue);
                break;
            case AutoHdrState.Disabled:
                flags.Set(AutoHdrFlag, AutoHdrDisabledValue);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown Auto HDR state.");
        }

        if (flags.IsEmpty)
        {
            _registry.DeleteValue(KeyPath, valueName);
        }
        else
        {
            _registry.SetString(KeyPath, valueName, flags.ToString());
        }
    }

    /// <summary>Drops the whole entry, every flag included. A no-op when there is none.</summary>
    public void Remove(string executablePath)
    {
        ValidateExecutablePath(executablePath);

        if (FindValueName(executablePath) is { } valueName)
        {
            _registry.DeleteValue(KeyPath, valueName);
        }
    }

    /// <summary>
    /// The global defaults entry (<c>AutoHDREnable</c>, <c>VRROptimizeEnable</c>,
    /// <c>SwapEffectUpgradeEnable</c>). Read-only on purpose: what these actually drive is not
    /// verified yet, and this app has no business guessing with a system-wide switch.
    /// </summary>
    public FlagString GetGlobalSettings() =>
        FlagString.Parse(_registry.GetString(KeyPath, GlobalSettingsValueName));

    internal static AutoHdrState ReadAutoHdrState(FlagString flags) =>
        flags.TryGetInt32(AutoHdrFlag, out var value)
            ? (value & 1) != 0 ? AutoHdrState.Enabled : AutoHdrState.Disabled
            : AutoHdrState.NotConfigured;

    /// <summary>
    /// Tells an application entry from the housekeeping values Windows parks in the same key
    /// (the global defaults, and GraphicsFeaturesNotificationConfig on Windows 11). Per-app entries
    /// are always named after a fully qualified executable path, so that is the test -- a deny-list
    /// of known names would go stale the moment Windows adds another one.
    /// </summary>
    private static bool IsApplicationValue(string valueName) => Path.IsPathFullyQualified(valueName);

    private static void ValidateExecutablePath(string executablePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        if (!IsApplicationValue(executablePath))
        {
            throw new ArgumentException(
                $"'{executablePath}' is not a fully qualified executable path; Windows keys these entries by full path.",
                nameof(executablePath));
        }
    }

    /// <summary>
    /// Finds the value name as the registry actually stores it, matching case-insensitively.
    /// </summary>
    private string? FindValueName(string executablePath)
    {
        foreach (var valueName in _registry.GetValueNames(KeyPath))
        {
            if (valueName.Equals(executablePath, StringComparison.OrdinalIgnoreCase))
            {
                return valueName;
            }
        }

        return null;
    }
}
