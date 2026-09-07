using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace ForceAutoHDR.Core;

/// <summary>
/// The <c>Key=Value;Key=Value;</c> soup that both Auto HDR mechanisms store in a single REG_SZ
/// value. Order and unknown keys are preserved on a round trip, which is the whole point:
/// Windows itself writes <c>SwapEffectUpgradeEnable</c>, <c>DXGIEffects</c>, <c>AppStatus</c> and
/// <c>GpuPreference</c> into the same string, and stomping them would silently change unrelated
/// per-app graphics settings.
/// Mutable, and deliberately dumb about persistence: changes reach the registry only when a store
/// writes the instance back.
/// </summary>
public sealed class FlagString
{
    private readonly List<Flag> _flags;

    /// <summary>
    /// Whether the source string ended with a separator. Windows writes
    /// <c>AutoHDREnable=2097;</c> under UserGpuPreferences but
    /// <c>BufferUpgradeOverride=1;BufferUpgradeEnable10Bit=1</c> without the trailing one, and a
    /// round trip should not rewrite what it did not mean to change.
    /// </summary>
    private readonly bool _trailingSeparator;

    /// <summary>Creates an empty flag string.</summary>
    /// <param name="trailingSeparator">
    /// Whether to render a trailing <c>;</c>. Windows uses one under UserGpuPreferences and none
    /// for <c>D3DBehaviors</c>, so new entries should match whichever they join.
    /// </param>
    public FlagString(bool trailingSeparator = true)
        : this([], trailingSeparator)
    {
    }

    private FlagString(List<Flag> flags, bool trailingSeparator)
    {
        _flags = flags;
        _trailingSeparator = trailingSeparator;
    }

    /// <summary>Number of flags.</summary>
    public int Count => _flags.Count;

    /// <summary>True when there is nothing left to write; the caller should delete the value instead.</summary>
    public bool IsEmpty => _flags.Count == 0;

    /// <summary>Flag keys in their original order and casing.</summary>
    public IEnumerable<string> Keys => _flags.Select(f => f.Key);

    /// <summary>The value of <paramref name="key"/>, or <see langword="null"/> when absent.</summary>
    public string? this[string key] => TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// Parses a raw registry string. <see langword="null"/>, empty and malformed input all yield a
    /// usable instance -- there is no such thing as a parse failure here, because whatever
    /// Windows wrote has to survive the round trip.
    /// </summary>
    public static FlagString Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return new FlagString();
        }

        var flags = new List<Flag>();
        foreach (var segment in raw.Split(';'))
        {
            if (segment.Length == 0)
            {
                continue;
            }

            var separator = segment.IndexOf('=');
            flags.Add(separator < 0
                ? new Flag(segment, null)
                : new Flag(segment[..separator], segment[(separator + 1)..]));
        }

        return new FlagString(flags, trailingSeparator: raw.EndsWith(';'));
    }

    /// <summary>Looks a flag up case-insensitively, as the registry does with its own names.</summary>
    public bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        foreach (var flag in _flags)
        {
            if (flag.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && flag.Value is not null)
            {
                value = flag.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>Reads a flag as an integer; false when it is missing or not a number.</summary>
    public bool TryGetInt32(string key, out int value)
    {
        if (TryGetValue(key, out var raw) &&
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Sets a flag, updating it in place (keeping the existing key's position and casing) or
    /// appending it at the end.
    /// </summary>
    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        for (var i = 0; i < _flags.Count; i++)
        {
            if (_flags[i].Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                _flags[i] = _flags[i] with { Value = value };
                return;
            }
        }

        _flags.Add(new Flag(key, value));
    }

    /// <summary>Removes every occurrence of a flag; returns whether anything was removed.</summary>
    public bool Remove(string key) =>
        _flags.RemoveAll(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) > 0;

    /// <summary>Renders the string exactly as it should go back into the registry.</summary>
    public override string ToString()
    {
        if (_flags.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var flag in _flags)
        {
            if (builder.Length > 0)
            {
                builder.Append(';');
            }

            builder.Append(flag.Key);
            if (flag.Value is not null)
            {
                builder.Append('=').Append(flag.Value);
            }
        }

        if (_trailingSeparator)
        {
            builder.Append(';');
        }

        return builder.ToString();
    }

    private readonly record struct Flag(string Key, string? Value);
}
