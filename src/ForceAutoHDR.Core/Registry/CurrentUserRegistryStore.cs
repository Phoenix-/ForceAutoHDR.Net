using Win32 = Microsoft.Win32;

namespace ForceAutoHDR.Core.Registry;

/// <summary>
/// The real thing: HKEY_CURRENT_USER. Both Auto HDR mechanisms live under HKCU, so no
/// elevation is ever needed -- see notes in the repo README.
/// </summary>
public sealed class CurrentUserRegistryStore : IRegistryStore
{
    /// <summary>Shared instance; the type is stateless and thread-safe.</summary>
    public static CurrentUserRegistryStore Instance { get; } = new();

    public bool KeyExists(string keyPath)
    {
        using var key = Win32.Registry.CurrentUser.OpenSubKey(keyPath);
        return key is not null;
    }

    public IReadOnlyList<string> GetSubKeyNames(string keyPath)
    {
        using var key = Win32.Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetSubKeyNames() ?? [];
    }

    public IReadOnlyList<string> GetValueNames(string keyPath)
    {
        using var key = Win32.Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValueNames() ?? [];
    }

    public string? GetString(string keyPath, string valueName)
    {
        using var key = Win32.Registry.CurrentUser.OpenSubKey(keyPath);
        // RegistryValueOptions.None expands REG_EXPAND_SZ; these values are plain REG_SZ, and
        // anything that is not a string is reported as absent rather than coerced.
        return key?.GetValue(valueName) as string;
    }

    public long? GetInt64(string keyPath, string valueName)
    {
        using var key = Win32.Registry.CurrentUser.OpenSubKey(keyPath);
        // A REG_QWORD surfaces as long; RegistryView aside, anything else is reported as absent.
        return key?.GetValue(valueName) as long?;
    }

    public void SetString(string keyPath, string valueName, string value)
    {
        using var key = Win32.Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue(valueName, value, Win32.RegistryValueKind.String);
    }

    public void DeleteValue(string keyPath, string valueName)
    {
        using var key = Win32.Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    public void CreateKey(string keyPath)
    {
        using var key = Win32.Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
    }

    public void DeleteKeyTree(string keyPath)
    {
        Win32.Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
    }
}
