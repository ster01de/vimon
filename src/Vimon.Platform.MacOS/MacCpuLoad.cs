namespace Vimon.Platform.MacOS;

internal static class MacCpuLoad
{
    /// <summary>
    /// Busy share of all ticks between two samples, in percent. Tick counters are 32-bit and wrap,
    /// so deltas use unchecked arithmetic. Returns null when no time has passed.
    /// </summary>
    public static double? Between(CpuLoadInfo previous, CpuLoadInfo current)
    {
        ulong user = unchecked(current.User - previous.User);
        ulong system = unchecked(current.System - previous.System);
        ulong nice = unchecked(current.Nice - previous.Nice);
        ulong idle = unchecked(current.Idle - previous.Idle);

        ulong busy = user + system + nice;
        ulong total = busy + idle;

        return total == 0 ? null : busy * 100.0 / total;
    }
}
