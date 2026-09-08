using System.Runtime.InteropServices;
using ForceAutoHDR.Core.Discovery.Interop;

namespace ForceAutoHDR.Core.Discovery;

/// <summary>
/// Finds what is rendering right now by reading the per-process GPU engine counters -- the same
/// numbers Task Manager's GPU column shows.
/// </summary>
/// <remarks>
/// <para>
/// The backstop for everything Game Bar missed: run it while the game is up and the real
/// executable falls out, whatever the launcher claims. No elevation needed.
/// </para>
/// <para>
/// The obvious alternative -- scan processes for a loaded <c>d3d11.dll</c> -- does not work at
/// all. Measured across 416 live processes, it matches explorer, Discord, Telegram, every browser
/// and every WinUI app, this one included. GPU *utilisation* is the signal; a loaded module is
/// not. See <c>notes/gameconfigstore-is-the-real-game-list.md</c>.
/// </para>
/// </remarks>
public sealed class RunningGameProbe : IGameCandidateSource
{
    /// <summary>The wildcard counter read, in its locale-independent English form.</summary>
    public const string CounterPath = @"\GPU Engine(*)\Utilization Percentage";

    private readonly string _windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    /// <summary>
    /// How much of the 3D engine a process must be using, summed across engines, to count as
    /// rendering. Five percent clears idle compositing without missing a game sitting in a menu.
    /// </summary>
    public double MinimumUtilizationPercent { get; init; } = 5.0;

    /// <summary>
    /// Gap between the two samples. Utilisation is a rate, so a single sample has nothing to
    /// compare against; a second is the interval Task Manager uses.
    /// </summary>
    public TimeSpan SampleInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    public GameCandidateOrigins Origin => GameCandidateOrigins.Running;

    /// <summary>
    /// Processes currently driving the GPU's 3D engine, busiest first.
    /// </summary>
    /// <remarks>
    /// Blocks for <see cref="SampleInterval"/> -- call it from a background thread. Returns empty
    /// rather than throwing when the counters are unavailable (performance counters disabled, a
    /// remote session, no GPU): discovery is advisory, and one dead source must not take the whole
    /// list down with it.
    /// </remarks>
    public IReadOnlyList<GameCandidate> GetCandidates()
    {
        var utilizationByProcess = SampleRender3DUtilization();
        if (utilizationByProcess.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var found = new List<(GameCandidate Candidate, double Utilization)>();
        foreach (var (processId, utilization) in utilizationByProcess)
        {
            if (utilization < MinimumUtilizationPercent)
            {
                continue;
            }

            if (ProcessImage.GetPath(processId) is not { Length: > 0 } path || IsUninteresting(path))
            {
                continue;
            }

            found.Add((new GameCandidate
            {
                ExecutablePath = path,
                Origins = GameCandidateOrigins.Running,
                // It is running, so it was last played now -- the only source that can say so.
                LastPlayedUtc = now,
                ExecutableExists = true,
            }, utilization));
        }

        found.Sort(static (a, b) => b.Utilization.CompareTo(a.Utilization));

        var candidates = new List<GameCandidate>(found.Count);
        foreach (var (candidate, _) in found)
        {
            candidates.Add(candidate);
        }

        return candidates;
    }

    /// <summary>
    /// The desktop itself is always drawing. Anything under the Windows directory is the
    /// compositor, the shell or a system app, and this probe's own host is never its own answer.
    /// </summary>
    private bool IsUninteresting(string executablePath) =>
        executablePath.StartsWith(_windowsDirectory, StringComparison.OrdinalIgnoreCase) ||
        executablePath.Equals(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Total 3D-engine utilisation per process id. A process owns one counter instance per
    /// physical engine, so the instances are summed.
    /// </summary>
    private Dictionary<uint, double> SampleRender3DUtilization()
    {
        var totals = new Dictionary<uint, double>();

        if (Pdh.OpenQuery(null, nint.Zero, out var query) != 0)
        {
            return totals;
        }

        try
        {
            if (Pdh.AddEnglishCounter(query, CounterPath, nint.Zero, out var counter) != 0 ||
                Pdh.CollectQueryData(query) != 0)
            {
                return totals;
            }

            Thread.Sleep(SampleInterval);

            if (Pdh.CollectQueryData(query) != 0)
            {
                return totals;
            }

            Accumulate(counter, totals);
        }
        finally
        {
            Pdh.CloseQuery(query);
        }

        return totals;
    }

    private static unsafe void Accumulate(nint counter, Dictionary<uint, double> totals)
    {
        // The first call reports the buffer size it wants, so PDH_MORE_DATA is the success path
        // here. Anything else -- PDH_NO_DATA included -- means nothing is rendering.
        uint bufferSize = 0;
        if (Pdh.GetFormattedCounterArray(counter, Pdh.FormatDouble, ref bufferSize, out _, nint.Zero) != Pdh.MoreData ||
            bufferSize == 0)
        {
            return;
        }

        var buffer = NativeMemory.Alloc(bufferSize);
        try
        {
            if (Pdh.GetFormattedCounterArray(counter, Pdh.FormatDouble, ref bufferSize, out var itemCount, (nint)buffer) != 0)
            {
                return;
            }

            var items = new ReadOnlySpan<Pdh.FormattedCounterValueItem>(buffer, (int)itemCount);
            foreach (ref readonly var item in items)
            {
                // Instance names point into this same buffer, so they are read before it is freed.
                if (item.Status != 0 || Marshal.PtrToStringUni(item.Name) is not { } instanceName)
                {
                    continue;
                }

                if (GpuEngineInstanceName.TryParseRender3D(instanceName, out var processId))
                {
                    totals[processId] = totals.GetValueOrDefault(processId) + item.Value;
                }
            }
        }
        finally
        {
            NativeMemory.Free(buffer);
        }
    }
}
