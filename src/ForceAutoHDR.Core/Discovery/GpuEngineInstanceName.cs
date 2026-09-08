using System.Globalization;

namespace ForceAutoHDR.Core.Discovery;

/// <summary>
/// Parses the instance names of the <c>GPU Engine</c> performance counter, which encode the
/// process and the engine into one string:
/// <c>pid_33100_luid_0x00000000_0x0000E885_phys_0_eng_0_engtype_3D</c>.
/// </summary>
/// <remarks>
/// Split out from <see cref="RunningGameProbe"/> so the parsing can be tested without a GPU: the
/// probe itself only ever returns something on a machine that is actually rendering.
/// </remarks>
internal static class GpuEngineInstanceName
{
    private const string ProcessIdPrefix = "pid_";

    /// <summary>The 3D engine suffix. Copy, video decode and encode engines carry other types.</summary>
    private const string Render3DSuffix = "_engtype_3D";

    /// <summary>
    /// Extracts the process id when the instance describes a 3D engine, which is the only engine
    /// type that means "this process is drawing". A process typically owns several 3D instances
    /// (one per physical engine), so callers are expected to sum them.
    /// </summary>
    public static bool TryParseRender3D(string instanceName, out uint processId)
    {
        processId = 0;
        if (!instanceName.StartsWith(ProcessIdPrefix, StringComparison.OrdinalIgnoreCase) ||
            !instanceName.EndsWith(Render3DSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var digits = instanceName.AsSpan(ProcessIdPrefix.Length);
        var end = digits.IndexOf('_');
        if (end > 0)
        {
            digits = digits[..end];
        }

        return uint.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out processId);
    }
}
