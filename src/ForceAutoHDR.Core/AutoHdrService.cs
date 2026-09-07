using ForceAutoHDR.Core.Registry;

namespace ForceAutoHDR.Core;

/// <summary>
/// Entry point of the library: the two Auto HDR mechanisms behind one merged list.
/// </summary>
/// <remarks>
/// Everything lives under HKCU, so nothing here needs elevation -- the original tool's admin
/// check was unnecessary. The stores stay public for the cases where the distinction matters.
/// </remarks>
public sealed class AutoHdrService(IRegistryStore registry)
{
    /// <summary>Per-app graphics preferences (the Settings page mechanism).</summary>
    public GpuPreferencesStore GpuPreferences { get; } = new(registry);

    /// <summary>Direct3D buffer-upgrade overrides (the forcing mechanism).</summary>
    public D3DBehaviorsStore D3DBehaviors { get; } = new(registry);

    /// <summary>Binds to the live registry of the current user.</summary>
    public static AutoHdrService ForCurrentUser() => new(CurrentUserRegistryStore.Instance);

    /// <summary>
    /// Every configured application, merged and sorted by display name.
    /// </summary>
    /// <remarks>
    /// The mechanisms are keyed differently -- one by full path, the other by bare file name -- so
    /// a single override can light up several paths at once, and that is reported honestly rather
    /// than hidden: every preference entry whose file name matches gets <c>IsForced</c>.
    /// </remarks>
    public IReadOnlyList<AutoHdrProfile> GetProfiles()
    {
        var overrides = D3DBehaviors.GetAll();
        var overridesByName = new Dictionary<string, D3DBehaviorEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in overrides)
        {
            // First subkey wins; duplicates for one executable are pathological but possible.
            overridesByName.TryAdd(entry.ExecutableName, entry);
        }

        var profiles = new List<AutoHdrProfile>();
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preference in GpuPreferences.GetAll())
        {
            overridesByName.TryGetValue(preference.ExecutableName, out var forcing);
            if (forcing is not null)
            {
                matched.Add(forcing.ExecutableName);
            }

            profiles.Add(new AutoHdrProfile
            {
                ExecutableName = preference.ExecutableName,
                ExecutablePath = preference.ExecutablePath,
                AutoHdr = preference.AutoHdr,
                IsForced = forcing?.IsAutoHdrForced ?? false,
                D3DSubKeyName = forcing?.SubKeyName,
            });
        }

        foreach (var entry in overrides)
        {
            if (matched.Contains(entry.ExecutableName))
            {
                continue;
            }

            profiles.Add(new AutoHdrProfile
            {
                ExecutableName = entry.ExecutableName,
                ExecutablePath = null,
                AutoHdr = AutoHdrState.NotConfigured,
                IsForced = entry.IsAutoHdrForced,
                D3DSubKeyName = entry.SubKeyName,
            });
        }

        profiles.Sort(static (a, b) =>
        {
            var byName = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            return byName != 0
                ? byName
                : string.Compare(a.ExecutablePath, b.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        });

        return profiles;
    }

    /// <summary>Sets the per-app toggle for an executable, keyed by its full path.</summary>
    public void SetAutoHdr(string executablePath, AutoHdrState state) =>
        GpuPreferences.SetAutoHdr(executablePath, state);

    /// <summary>Adds or removes the forcing override; accepts a full path or a bare file name.</summary>
    public void SetForced(string executable, bool forced) =>
        D3DBehaviors.SetForced(executable, forced);

    /// <summary>
    /// Removes every trace of an application: its preferences entry and any forcing override for
    /// its file name.
    /// </summary>
    public void Remove(string executablePath)
    {
        GpuPreferences.Remove(executablePath);
        D3DBehaviors.SetForced(executablePath, forced: false);
    }
}
