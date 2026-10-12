namespace Vimon.Core;

public static class TopProcesses
{
    // CPU is compared as shown (one decimal), so noise below that does not reorder rows.
    private const int CpuSortDecimals = 1;
    // Reads are scheduled by the caller's clock; a few milliseconds early still count as due.
    private static readonly TimeSpan IntervalTolerance = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The busiest processes: CPU first (unknown last), then memory, then name and pid, so rows
    /// keep their order while values are equal.
    /// </summary>
    public static IReadOnlyList<ProcessUsage> Select(IEnumerable<ProcessUsage> usages, int count) =>
        usages
            .OrderByDescending(u => u.CpuPct.HasValue)
            .ThenByDescending(u => Math.Round(u.CpuPct ?? 0, CpuSortDecimals))
            .ThenByDescending(u => u.ResidentBytes)
            .ThenBy(u => u.Name, StringComparer.Ordinal)
            .ThenBy(u => u.Pid)
            .Take(count)
            .ToList();

    /// <summary>The processes moving the most bytes to or from storage; idle ones are left out.</summary>
    public static IReadOnlyList<ProcessUsage> SelectByDisk(IEnumerable<ProcessUsage> usages, int count) =>
        usages
            .Where(u => u.DiskBytesPerSec > 0)
            .OrderByDescending(u => u.DiskBytesPerSec)
            .ThenBy(u => u.Name, StringComparer.Ordinal)
            .ThenBy(u => u.Pid)
            .Take(count)
            .ToList();

    /// <summary>
    /// Processes are read less often than other sensors. The first two reads are back to back so a
    /// one-shot run gets CPU values; after that one read per <paramref name="interval"/>.
    /// </summary>
    public static bool IsSampleDue(int samplesTaken, TimeSpan sinceLastSample, TimeSpan interval) =>
        samplesTaken < 2 || sinceLastSample >= interval - IntervalTolerance;
}
