using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Vimon.Core;

namespace Vimon.Platform.MacOS.Tests;

public class MacProcessesTests
{
    private const double AppleSiliconNsPerTick = 125.0 / 3;
    private const int BusyMs = 300;

    /// <summary>A proc_taskallinfo buffer laid out like the kernel writes it.</summary>
    private static byte[] TaskAllInfo(string name, string comm, long startSec, long startUsec, ulong resident, ulong userTicks, ulong systemTicks)
    {
        var info = new byte[MacProcessInfo.Length];
        Encoding.ASCII.GetBytes(comm).CopyTo(info, 48);
        Encoding.ASCII.GetBytes(name).CopyTo(info, 64);
        BinaryPrimitives.WriteInt64LittleEndian(info.AsSpan(120), startSec);
        BinaryPrimitives.WriteInt64LittleEndian(info.AsSpan(128), startUsec);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(144), resident);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(152), userTicks);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(160), systemTicks);
        return info;
    }

    [Fact]
    public void ReadsNameStartTimeMemoryAndCpuTimeAtTheirOffsets()
    {
        // 4 800 000 Mach ticks at 125/3 ns per tick: exactly 200 ms.
        var info = TaskAllInfo("Google Chrome Helper", "Google Chrome H", 1_791_787_689, 250, 1_097_728, 4_800_000, 0);

        var parsed = MacProcessInfo.Parse(4242, info, AppleSiliconNsPerTick);

        Assert.Equal(new ProcessKey(4242, 1_791_787_689_000_250), parsed.Key);
        Assert.Equal("Google Chrome Helper", parsed.Name);
        Assert.Equal(TimeSpan.FromMilliseconds(200), parsed.CpuTime);
        Assert.Equal(1_097_728UL, parsed.ResidentBytes);
    }

    [Fact]
    public void AddsUserAndSystemTime()
    {
        var info = TaskAllInfo("x", "x", 0, 0, 0, userTicks: 1_000, systemTicks: 2_000);

        Assert.Equal(TimeSpan.FromTicks(30), MacProcessInfo.Parse(1, info, nanosecondsPerMachTick: 1).CpuTime);
    }

    [Fact]
    public void FallsBackToTheShortNameAndThenToThePid()
    {
        Assert.Equal("launchd", MacProcessInfo.Parse(1, TaskAllInfo("", "launchd", 0, 0, 0, 0, 0), 1).Name);
        Assert.Equal("pid 7", MacProcessInfo.Parse(7, TaskAllInfo("", "", 0, 0, 0, 0, 0), 1).Name);
    }

    [MacOSFact]
    public void ReadsTheTimebaseOfThisMac()
    {
        Assert.InRange(MacProcesses.ReadTimebase(), 1, 1000);
    }

    [MacOSFact]
    public void SeesItsOwnProcessButNotLaunchd()
    {
        var processes = new MacProcesses().ReadAll();

        var self = Assert.Single(processes, p => p.Key.Pid == Environment.ProcessId);
        Assert.NotEmpty(self.Name);
        Assert.True(self.ResidentBytes > 0);
        Assert.DoesNotContain(processes, p => p.Key.Pid == 1);
    }

    [MacOSFact]
    public void MeasuresCpuOfABusyProcess()
    {
        var reader = new MacProcesses();
        var clock = Stopwatch.StartNew();
        var (_, tracker) = ProcessCpuTracker.Empty.Advance(reader.ReadAll(), clock.Elapsed, Environment.ProcessorCount);

        var busy = Stopwatch.StartNew();
        double sink = 0;
        while (busy.ElapsedMilliseconds < BusyMs)
            sink += Math.Sqrt(busy.ElapsedTicks);

        var (usages, _) = tracker.Advance(reader.ReadAll(), clock.Elapsed, Environment.ProcessorCount);

        var self = Assert.Single(usages, u => u.Pid == Environment.ProcessId);
        Assert.True(self.CpuPct > 0, $"CPU {self.CpuPct} (sink {sink})");
    }
}
