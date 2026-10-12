namespace Vimon.Core;

/// <summary>A process across samples. The start time keeps a reused pid from inheriting old CPU time.</summary>
public readonly record struct ProcessKey(int Pid, long StartTicks);

/// <summary>Cumulative CPU time and memory of one process at one moment.</summary>
public sealed record ProcessTimes(ProcessKey Key, string Name, TimeSpan CpuTime, ulong ResidentBytes);

/// <param name="CpuPct">Share of the whole machine, 0–100 like the CPU load; null until two samples exist.</param>
public sealed record ProcessUsage(int Pid, string Name, double? CpuPct, ulong ResidentBytes);

/// <summary>Which processes a backend can see.</summary>
public enum ProcessScope
{
    /// <summary>Processes are not read (not implemented on this platform, or turned off).</summary>
    None,

    /// <summary>Only the user's own processes; the OS hides the rest without root (macOS).</summary>
    CurrentUser,

    All
}
