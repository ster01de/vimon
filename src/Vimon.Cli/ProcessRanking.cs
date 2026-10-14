using Vimon.Core;

namespace Vimon.Cli;

/// <summary>
/// Keeps process rows in place between samples: a process takes another one's row only when it is
/// clearly busier, and a process that leaves is replaced in its own row. Pure.
/// </summary>
internal static class ProcessRanking
{
    /// <summary>The list is only re-sorted when one process is this many percentage points ahead of one above it.</summary>
    public const double OvertakeMarginPct = 5.0;

    /// <summary>Another process takes the "Busiest" place only when it moves this many times more data.</summary>
    public const double BusiestOvertakeRatio = 1.5;

    /// <summary><paramref name="current"/> in the order of <paramref name="shown"/> as far as the margin allows.</summary>
    public static IReadOnlyList<ProcessUsage> Arrange(IReadOnlyList<ProcessUsage> shown, IReadOnlyList<ProcessUsage> current)
    {
        if (shown.Count == 0)
            return current;

        var newcomers = new Queue<ProcessUsage>(current
            .Where(process => !shown.Any(old => IsSame(old, process)))
            .OrderByDescending(Cpu));

        // A process that left hands its row to the busiest newcomer; rows nobody takes close up.
        var rows = shown
            .Select(old => current.FirstOrDefault(process => IsSame(old, process))
                           ?? (newcomers.Count > 0 ? newcomers.Dequeue() : null))
            .OfType<ProcessUsage>()
            .ToList()
            .Concat(newcomers)
            .ToList();

        return IsClearlyOutOfOrder(rows) ? rows.OrderByDescending(Cpu).ToList() : rows;
    }

    /// <summary>
    /// <paramref name="top"/> with the previous busiest process kept first while it still moves data and
    /// no other one moves <see cref="BusiestOvertakeRatio"/> times as much.
    /// </summary>
    public static IReadOnlyList<ProcessUsage> KeepBusiest(ProcessUsage? previous, IReadOnlyList<ProcessUsage> top)
    {
        if (previous is null || top.Count == 0)
            return top;

        var kept = top.FirstOrDefault(process => IsSame(previous, process));

        if (kept is null || kept.DiskBytesPerSec <= 0 || top[0].DiskBytesPerSec >= kept.DiskBytesPerSec * BusiestOvertakeRatio)
            return top;

        return top.Where(process => !ReferenceEquals(process, kept)).Prepend(kept).ToList();
    }

    // The name guards against a pid the OS has handed to another program in the meantime.
    private static bool IsSame(ProcessUsage a, ProcessUsage b) => a.Pid == b.Pid && a.Name == b.Name;

    // A process without a CPU reading never moves up.
    private static double Cpu(ProcessUsage process) => process.CpuPct ?? -1;

    private static bool IsClearlyOutOfOrder(IReadOnlyList<ProcessUsage> rows)
    {
        for (int below = 1; below < rows.Count; below++)
        {
            for (int above = 0; above < below; above++)
            {
                if (Cpu(rows[below]) > Cpu(rows[above]) + OvertakeMarginPct)
                    return true;
            }
        }

        return false;
    }
}
