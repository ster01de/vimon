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

    private const int PidHeadroom = 64;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(2);

    private readonly double _nanosecondsPerTick = ReadTimebase();
    private readonly byte[] _info = new byte[MacProcessInfo.Length];
    private readonly long _start = Stopwatch.GetTimestamp();
    private ProcessCpuTracker _tracker = ProcessCpuTracker.Empty;
    private IReadOnlyList<ProcessUsage> _top = [];
    private int _samplesTaken;
    private TimeSpan _sampledAt;

    public IReadOnlyList<ProcessUsage> Read()
    {
        var now = Stopwatch.GetElapsedTime(_start);

        if (!TopProcesses.IsSampleDue(_samplesTaken, now - _sampledAt, SampleInterval))
            return _top;

        var (usages, next) = _tracker.Advance(ReadAll(), now, Environment.ProcessorCount);
        _tracker = next;
        _top = TopProcesses.Select(usages, TopCount);
        _samplesTaken++;
        _sampledAt = now;
        return _top;
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
                processes.Add(MacProcessInfo.Parse(pid, _info, _nanosecondsPerTick));
        }

        return processes;
    }

    /// <summary>Nanoseconds per Mach tick: 125/3 on Apple Silicon, 1 on Intel Macs.</summary>
    internal static double ReadTimebase() =>
        MacNative.mach_timebase_info(out var timebase) == MacNative.KernSuccess && timebase.Denom != 0
            ? (double)timebase.Numer / timebase.Denom
            : 1;
}
