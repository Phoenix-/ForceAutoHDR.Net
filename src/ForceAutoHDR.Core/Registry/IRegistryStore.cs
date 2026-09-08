namespace ForceAutoHDR.Core.Registry;

/// <summary>
/// A minimal, path-addressed view of one registry hive (HKCU for this app).
/// </summary>
/// <remarks>
/// Path-addressed instead of handle-based on purpose: callers never juggle disposable keys,
/// and the in-memory fake stays a dictionary. Key and value names are compared
/// case-insensitively, the way the registry itself does it. Only REG_SZ values are modelled --
/// both Auto HDR mechanisms store strings.
/// All paths are relative to the hive root and use backslashes, e.g. <c>Software\Microsoft\Direct3D</c>.
/// </remarks>
public interface IRegistryStore
{
    /// <summary>Returns <see langword="true"/> if the key exists.</summary>
    bool KeyExists(string keyPath);

    /// <summary>Names of the immediate child keys; empty when the key does not exist.</summary>
    IReadOnlyList<string> GetSubKeyNames(string keyPath);

    /// <summary>Names of the values in the key; empty when the key does not exist.</summary>
    IReadOnlyList<string> GetValueNames(string keyPath);

    /// <summary>The string value, or <see langword="null"/> when the key, the value, or the REG_SZ type is missing.</summary>
    string? GetString(string keyPath, string valueName);

    /// <summary>
    /// The REG_QWORD value, or <see langword="null"/> when the key, the value, or that type is
    /// missing. Read-only: the one QWORD this app reads is <c>GameConfigStore</c>'s
    /// <c>LastAccessed</c>, which belongs to Game Bar and is never written back.
    /// </summary>
    long? GetInt64(string keyPath, string valueName);

    /// <summary>Writes a REG_SZ value, creating the key (and its parents) if needed.</summary>
    void SetString(string keyPath, string valueName, string value);

    /// <summary>Deletes a value; a no-op when the key or value does not exist.</summary>
    void DeleteValue(string keyPath, string valueName);

    /// <summary>Creates the key and its parents; a no-op when it already exists.</summary>
    void CreateKey(string keyPath);

    /// <summary>Deletes a key and everything under it; a no-op when it does not exist.</summary>
    void DeleteKeyTree(string keyPath);
}
