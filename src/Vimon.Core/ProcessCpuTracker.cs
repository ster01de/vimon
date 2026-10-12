using System.Collections.Immutable;

namespace Vimon.Core;

/// <summary>
/// Turns cumulative CPU time per process into a share of the whole machine. Immutable:
/// <see cref="Advance"/> returns the usages together with the tracker for the next sample.
/// </summary>
public sealed class ProcessCpuTracker
{
    private const double MaxPct = 100;

    public static readonly ProcessCpuTracker Empty = new(ImmutableDictionary<ProcessKey, TimeSpan>.Empty, sampledAt: null);

    private readonly ImmutableDictionary<ProcessKey, TimeSpan> _previous;
    private readonly TimeSpan? _sampledAt;

    private ProcessCpuTracker(ImmutableDictionary<ProcessKey, TimeSpan> previous, TimeSpan? sampledAt)
    {
        _previous = previous;
        _sampledAt = sampledAt;
    }

    /// <param name="now">Monotonic time of this sample.</param>
    /// <param name="coreCount">Logical cores; one busy thread on 8 cores counts as 12.5 %.</param>
    /// <returns>
    /// One usage per process. CPU is null on the first sample of a process, when no time has
    /// passed, or when its CPU time went backwards.
    /// </returns>
    public (IReadOnlyList<ProcessUsage> Usages, ProcessCpuTracker Next) Advance(
        IReadOnlyList<ProcessTimes> samples, TimeSpan now, int coreCount)
    {
        double? capacitySeconds = _sampledAt is TimeSpan before && now > before && coreCount > 0
            ? (now - before).TotalSeconds * coreCount
            : null;

        var unique = samples.GroupBy(s => s.Key).Select(group => group.Last()).ToList();
        var usages = unique
            .Select(s => new ProcessUsage(s.Key.Pid, s.Name, CpuPct(s, capacitySeconds), s.ResidentBytes))
            .ToList();
        var next = unique.ToImmutableDictionary(s => s.Key, s => s.CpuTime);

        return (usages, new ProcessCpuTracker(next, now));
    }

    private double? CpuPct(ProcessTimes sample, double? capacitySeconds)
    {
        if (capacitySeconds is not double capacity
            || !_previous.TryGetValue(sample.Key, out var before)
            || sample.CpuTime < before)
        {
            return null;
        }

        return Math.Clamp((sample.CpuTime - before).TotalSeconds / capacity * 100, 0, MaxPct);
    }
}
