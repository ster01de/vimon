namespace Vimon.Core;

public static class TopProcesses
{
    /// <summary>
    /// The busiest processes: CPU first (unknown last), then memory, then name and pid, so rows
    /// keep their order while values are equal.
    /// </summary>
    public static IReadOnlyList<ProcessUsage> Select(IEnumerable<ProcessUsage> usages, int count) =>
        usages
            .OrderByDescending(u => u.CpuPct.HasValue)
            .ThenByDescending(u => u.CpuPct ?? 0)
            .ThenByDescending(u => u.ResidentBytes)
            .ThenBy(u => u.Name, StringComparer.Ordinal)
            .ThenBy(u => u.Pid)
            .Take(count)
            .ToList();

    /// <summary>
    /// Processes are read less often than other sensors. The first two reads are back to back so a
    /// one-shot run gets CPU values; after that one read per <paramref name="interval"/>.
    /// </summary>
    public static bool IsSampleDue(int samplesTaken, TimeSpan sinceLastSample, TimeSpan interval) =>
        samplesTaken < 2 || sinceLastSample >= interval;
}
