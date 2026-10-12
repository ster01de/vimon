using System.Collections.Immutable;

namespace Vimon.Core;

/// <summary>
/// Turns cumulative CPU time and disk bytes per process into a share of the whole machine and
/// bytes per second. Immutable: <see cref="Advance"/> returns the usages together with the tracker
/// for the next sample.
/// </summary>
public sealed class ProcessCpuTracker
{
    private const double MaxPct = 100;

    public static readonly ProcessCpuTracker Empty = new(ImmutableDictionary<ProcessKey, ProcessTimes>.Empty, sampledAt: null);

    private readonly ImmutableDictionary<ProcessKey, ProcessTimes> _previous;
    private readonly TimeSpan? _sampledAt;

    private ProcessCpuTracker(ImmutableDictionary<ProcessKey, ProcessTimes> previous, TimeSpan? sampledAt)
    {
        _previous = previous;
        _sampledAt = sampledAt;
    }

    /// <param name="now">Monotonic time of this sample.</param>
    /// <param name="coreCount">Logical cores; one busy thread on 8 cores counts as 12.5 %.</param>
    /// <returns>
    /// One usage per process. A value is null on the first sample of a process, when no time has
    /// passed, or when its counter went backwards.
    /// </returns>
    public (IReadOnlyList<ProcessUsage> Usages, ProcessCpuTracker Next) Advance(
        IReadOnlyList<ProcessTimes> samples, TimeSpan now, int coreCount)
    {
        double? elapsedSeconds = _sampledAt is TimeSpan before && now > before ? (now - before).TotalSeconds : null;

        var unique = samples.GroupBy(s => s.Key).Select(group => group.Last()).ToList();
        var usages = unique.Select(s => Usage(s, elapsedSeconds, coreCount)).ToList();
        var next = unique.ToImmutableDictionary(s => s.Key);

        return (usages, new ProcessCpuTracker(next, now));
    }

    private ProcessUsage Usage(ProcessTimes sample, double? elapsedSeconds, int coreCount)
    {
        var usage = new ProcessUsage(sample.Key.Pid, sample.Name, CpuPct: null, sample.ResidentBytes);

        if (elapsedSeconds is not double seconds || !_previous.TryGetValue(sample.Key, out var before))
            return usage;

        return usage with
        {
            CpuPct = CpuPct(before.CpuTime, sample.CpuTime, seconds, coreCount),
            DiskReadBytesPerSec = PerSecond(before.DiskReadBytes, sample.DiskReadBytes, seconds),
            DiskWriteBytesPerSec = PerSecond(before.DiskWriteBytes, sample.DiskWriteBytes, seconds)
        };
    }

    private static double? CpuPct(TimeSpan before, TimeSpan after, double seconds, int coreCount)
    {
        if (coreCount <= 0 || after < before)
            return null;

        return Math.Clamp((after - before).TotalSeconds / (seconds * coreCount) * 100, 0, MaxPct);
    }

    private static double? PerSecond(ulong? before, ulong? after, double seconds) =>
        before is ulong b && after is ulong a && a >= b ? (a - b) / seconds : null;
}
