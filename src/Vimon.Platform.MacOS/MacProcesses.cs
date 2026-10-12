using System.Diagnostics;
using Vimon.Core;

namespace Vimon.Platform.MacOS;

/// <summary>
/// The busiest processes of the current user via libproc. Without root, macOS refuses CPU time and
/// memory of other users' processes (EPERM), so system daemons such as WindowServer never appear.
/// One pass over ~400 processes takes under 1 ms on an M2; it still only runs every 2 s.
/// </summary>
internal sealed class MacProcesses
{
    public const int TopCount = 5;
    public const int TopDiskCount = 2;

    private const int PidHeadroom = 64;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(2);

    private readonly double _nanosecondsPerTick = ReadTimebase();
    private readonly byte[] _info = new byte[MacProcessInfo.Length];
    private readonly long _start = Stopwatch.GetTimestamp();
    private ProcessCpuTracker _tracker = ProcessCpuTracker.Empty;
    private readonly byte[] _rusage = new byte[MacRusage.Length];
    private MacProcessReading _last = MacProcessReading.None;
    private int _samplesTaken;
    private TimeSpan _sampledAt;

    public MacProcessReading Read()
    {
        var now = Stopwatch.GetElapsedTime(_start);

        if (!TopProcesses.IsSampleDue(_samplesTaken, now - _sampledAt, SampleInterval))
            return _last;

        var (usages, next) = _tracker.Advance(ReadAll(), now, Environment.ProcessorCount);
        _tracker = next;
        _last = new MacProcessReading(TopProcesses.Select(usages, TopCount), TopProcesses.SelectByDisk(usages, TopDiskCount));
        _samplesTaken++;
        _sampledAt = now;
        return _last;
    }

    internal List<ProcessTimes> ReadAll()
    {
        int count = MacNative.proc_listallpids(null, 0);

        if (count <= 0)
            return [];

        // Processes can start between the two calls, so leave headroom.
        var pids = new int[count + PidHeadroom];
        count = MacNative.proc_listallpids(pids, pids.Length * sizeof(int));

        var processes = new List<ProcessTimes>(Math.Max(count, 0));

        foreach (int pid in pids.AsSpan(0, Math.Clamp(count, 0, pids.Length)))
        {
            // 0 bytes: the process belongs to another user or has just exited.
            if (pid > 0 && MacNative.proc_pidinfo(pid, MacProcessInfo.Flavor, 0, _info, _info.Length) == _info.Length)
                processes.Add(WithDiskBytes(pid, MacProcessInfo.Parse(pid, _info, _nanosecondsPerTick)));
        }

        return processes;
    }

    /// <summary>Same pid list, one more call: disk bytes are only read where the task info was readable.</summary>
    private ProcessTimes WithDiskBytes(int pid, ProcessTimes times)
    {
        if (MacNative.proc_pid_rusage(pid, MacRusage.Flavor, _rusage) != 0)
            return times;

        var (read, written) = MacRusage.Parse(_rusage);
        return times with { DiskReadBytes = read, DiskWriteBytes = written };
    }

    /// <summary>Nanoseconds per Mach tick: 125/3 on Apple Silicon, 1 on Intel Macs.</summary>
    internal static double ReadTimebase() =>
        MacNative.mach_timebase_info(out var timebase) == MacNative.KernSuccess && timebase.Denom != 0
            ? (double)timebase.Numer / timebase.Denom
            : 1;
}

/// <summary>The busiest processes by CPU and by disk I/O from one pass over the process list.</summary>
internal sealed record MacProcessReading(IReadOnlyList<ProcessUsage> Top, IReadOnlyList<ProcessUsage> TopDisk)
{
    public static readonly MacProcessReading None = new([], []);
}
