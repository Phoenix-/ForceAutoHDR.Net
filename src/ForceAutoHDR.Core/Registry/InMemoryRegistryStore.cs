namespace ForceAutoHDR.Core.Registry;

/// <summary>
/// A dictionary-backed <see cref="IRegistryStore"/>: the substrate for unit tests, for
/// design-time data in the UI, and for a dry run that shows what would be written.
/// </summary>
/// <remarks>Not thread-safe.</remarks>
public sealed class InMemoryRegistryStore : IRegistryStore
{
    // keyPath (normalized) -> valueName -> value. A key with no values still gets an entry,
    // because an empty key is a real thing in the registry.
    private readonly Dictionary<string, Dictionary<string, string>> _keys =
        new(StringComparer.OrdinalIgnoreCase);

    public bool KeyExists(string keyPath) => _keys.ContainsKey(Normalize(keyPath));

    public IReadOnlyList<string> GetSubKeyNames(string keyPath)
    {
        var prefix = Normalize(keyPath);
        if (!_keys.ContainsKey(prefix))
        {
            return [];
        }

        var prefixWithSeparator = prefix.Length == 0 ? string.Empty : prefix + '\\';
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in _keys.Keys)
        {
            if (path.Length <= prefixWithSeparator.Length ||
                !path.StartsWith(prefixWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rest = path[prefixWithSeparator.Length..];
            var separator = rest.IndexOf('\\');
            var child = separator < 0 ? rest : rest[..separator];
            if (seen.Add(child))
            {
                names.Add(child);
            }
        }

        return names;
    }

    public IReadOnlyList<string> GetValueNames(string keyPath) =>
        _keys.TryGetValue(Normalize(keyPath), out var values) ? [.. values.Keys] : [];

    public string? GetString(string keyPath, string valueName) =>
        _keys.TryGetValue(Normalize(keyPath), out var values) && values.TryGetValue(valueName, out var value)
            ? value
            : null;

    public void SetString(string keyPath, string valueName, string value)
    {
        var values = GetOrCreate(Normalize(keyPath));
        // Mirrors the registry: rewriting an existing value keeps the name's original casing.
        values[valueName] = value;
    }

    public void DeleteValue(string keyPath, string valueName)
    {
        if (_keys.TryGetValue(Normalize(keyPath), out var values))
        {
            values.Remove(valueName);
        }
    }

    public void CreateKey(string keyPath) => GetOrCreate(Normalize(keyPath));

    public void DeleteKeyTree(string keyPath)
    {
        var path = Normalize(keyPath);
        var prefix = path + '\\';
        var doomed = _keys.Keys
            .Where(k => k.Equals(path, StringComparison.OrdinalIgnoreCase) ||
                        k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var key in doomed)
        {
            _keys.Remove(key);
        }
    }

    private Dictionary<string, string> GetOrCreate(string normalizedPath)
    {
        if (_keys.TryGetValue(normalizedPath, out var existing))
        {
            return existing;
        }

        // The registry creates the whole chain of parents; so does this.
        var segments = normalizedPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var path = string.Empty;
        Dictionary<string, string>? current = null;
        foreach (var segment in segments)
        {
            path = path.Length == 0 ? segment : path + '\\' + segment;
            if (!_keys.TryGetValue(path, out current))
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _keys[path] = current;
            }
        }

        return current ?? GetRoot();
    }

    private Dictionary<string, string> GetRoot()
    {
        if (!_keys.TryGetValue(string.Empty, out var root))
        {
            root = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _keys[string.Empty] = root;
        }

        return root;
    }

    private static string Normalize(string keyPath) => keyPath.Trim('\\');
}
