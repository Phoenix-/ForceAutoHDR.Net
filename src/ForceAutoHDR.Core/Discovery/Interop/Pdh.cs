using System.Runtime.InteropServices;

namespace ForceAutoHDR.Core.Discovery.Interop;

/// <summary>
/// The slice of the Performance Data Helper API needed to read one wildcard counter.
/// </summary>
/// <remarks>
/// <c>PdhAddEnglishCounter</c> rather than <c>PdhAddCounter</c> on purpose: counter paths are
/// localised, and <c>\GPU Engine(*)\Utilization Percentage</c> does not exist under that name on a
/// non-English Windows. The English variant looks the name up by index and works everywhere.
/// </remarks>
internal static partial class Pdh
{
    /// <summary>Format the counter as a <see cref="double"/>.</summary>
    public const uint FormatDouble = 0x00000200;

    /// <summary>The buffer was too small; the required size has been written back.</summary>
    public const uint MoreData = 0x800007D2;

    /// <summary>The query returned no instances at all.</summary>
    public const uint NoData = 0x800007D5;

    /// <summary>One instance of a wildcard counter: its name, and the formatted value.</summary>
    /// <remarks>
    /// Sequential layout with natural alignment matches the native
    /// <c>PDH_FMT_COUNTERVALUE_ITEM_W</c> on both 32- and 64-bit: the union in
    /// <c>PDH_FMT_COUNTERVALUE</c> is 8-byte aligned, which is exactly what the runtime does for
    /// the <see cref="Value"/> field after the 4-byte <see cref="Status"/>. Only the double arm of
    /// the union is declared, because only <see cref="FormatDouble"/> is ever requested.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct FormattedCounterValueItem
    {
        /// <summary>Pointer to the instance name, into the same buffer the item lives in.</summary>
        public nint Name;

        /// <summary>Per-instance status; anything non-zero means <see cref="Value"/> is not usable.</summary>
        public uint Status;

        /// <summary>The formatted value.</summary>
        public double Value;
    }

    [LibraryImport("pdh.dll", EntryPoint = "PdhOpenQueryW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint OpenQuery(string? dataSource, nint userData, out nint query);

    [LibraryImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint AddEnglishCounter(nint query, string counterPath, nint userData, out nint counter);

    [LibraryImport("pdh.dll", EntryPoint = "PdhCollectQueryData")]
    public static partial uint CollectQueryData(nint query);

    [LibraryImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW")]
    public static partial uint GetFormattedCounterArray(
        nint counter,
        uint format,
        ref uint bufferSize,
        out uint itemCount,
        nint buffer);

    [LibraryImport("pdh.dll", EntryPoint = "PdhCloseQuery")]
    public static partial uint CloseQuery(nint query);
}
