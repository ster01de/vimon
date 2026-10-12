using Vimon.Core;

namespace Vimon.Core.Tests;

public class ProcessCpuTrackerTests
{
    private const int Cores = 8;
    private static readonly TimeSpan Start = TimeSpan.FromSeconds(10);
    private static readonly ProcessKey Xcode = new(100, StartTicks: 1);

    private static ProcessTimes Sample(ProcessKey key, double cpuSeconds, ulong resident = 1000) =>
        new(key, "Xcode", TimeSpan.FromSeconds(cpuSeconds), resident);

    private static ProcessUsage Second(ProcessTimes first, ProcessTimes second, double elapsedSeconds = 1)
    {
        var (_, tracker) = ProcessCpuTracker.Empty.Advance([first], Start, Cores);
        var (usages, _) = tracker.Advance([second], Start + TimeSpan.FromSeconds(elapsedSeconds), Cores);
        return Assert.Single(usages);
    }

    [Fact]
    public void ReportsNoCpuOnTheFirstSample()
    {
        var (usages, _) = ProcessCpuTracker.Empty.Advance([Sample(Xcode, 5)], Start, Cores);

        Assert.Equal(new ProcessUsage(100, "Xcode", null, 1000), Assert.Single(usages));
    }

    [Fact]
    public void ReportsCpuAsAShareOfTheWholeMachine()
    {
        // Two CPU seconds in one second on eight cores: a quarter of the machine.
        var usage = Second(Sample(Xcode, 5), Sample(Xcode, 7));

        Assert.Equal(25, usage.CpuPct);
    }

    [Fact]
    public void DoesNotInheritCpuTimeWhenAPidIsReused()
    {
        var usage = Second(Sample(Xcode, 50), Sample(Xcode with { StartTicks = 2 }, 1));

        Assert.Null(usage.CpuPct);
    }

    [Fact]
    public void ReportsNoCpuWhenCpuTimeWentBackwards()
    {
        Assert.Null(Second(Sample(Xcode, 7), Sample(Xcode, 5)).CpuPct);
    }

    [Fact]
    public void ReportsNoCpuWhenNoTimePassed()
    {
        Assert.Null(Second(Sample(Xcode, 5), Sample(Xcode, 7), elapsedSeconds: 0).CpuPct);
    }

    [Fact]
    public void CapsCpuAtTheWholeMachine()
    {
        Assert.Equal(100, Second(Sample(Xcode, 0), Sample(Xcode, 100)).CpuPct);
    }

    [Fact]
    public void CountsAProcessListedTwiceOnlyOnce()
    {
        var (usages, _) = ProcessCpuTracker.Empty.Advance([Sample(Xcode, 1), Sample(Xcode, 2)], Start, Cores);

        Assert.Single(usages);
    }
}

public class TopProcessesTests
{
    private static ProcessUsage Usage(int pid, string name, double? cpu, ulong resident = 0) => new(pid, name, cpu, resident);

    [Fact]
    public void SortsByCpuThenMemoryThenNameAndPid()
    {
        var usages = new[]
        {
            Usage(1, "idle", null, 9_000),
            Usage(2, "b", 10, 100),
            Usage(3, "a", 10, 100),
            Usage(4, "big", 10, 500),
            Usage(5, "hot", 50),
            Usage(6, "a", 10, 100)
        };

        var names = TopProcesses.Select(usages, 10).Select(u => $"{u.Name}#{u.Pid}");

        Assert.Equal(new[] { "hot#5", "big#4", "a#3", "a#6", "b#2", "idle#1" }, names);
    }

    [Fact]
    public void IgnoresCpuNoiseBelowTheShownDecimal()
    {
        var names = TopProcesses.Select([Usage(1, "b", 0.04, 10), Usage(2, "a", 0.01, 20)], 2).Select(u => u.Name);

        Assert.Equal(new[] { "a", "b" }, names);
    }

    [Fact]
    public void KeepsOnlyTheRequestedCount()
    {
        var usages = Enumerable.Range(1, 10).Select(i => Usage(i, $"p{i}", i));

        Assert.Equal(3, TopProcesses.Select(usages, 3).Count);
        Assert.Empty(TopProcesses.Select([], 3));
    }

    [Theory]
    [InlineData(0, 0.0, true)]
    [InlineData(1, 0.5, true)]
    [InlineData(2, 0.5, false)]
    [InlineData(5, 2.0, true)]
    [InlineData(5, 1.98, true)]
    [InlineData(5, 1.5, false)]
    public void SamplesTwiceRightAwayThenOncePerInterval(int samplesTaken, double secondsSinceLast, bool expected) =>
        Assert.Equal(expected, TopProcesses.IsSampleDue(samplesTaken, TimeSpan.FromSeconds(secondsSinceLast), TimeSpan.FromSeconds(2)));
}

public class ProcessNameTests
{
    [Theory]
    [InlineData("Xcode", "Xcode")]
    [InlineData("evil\u001b[31mred", "evil[31mred")]
    [InlineData("  spaced  ", "spaced")]
    [InlineData("spoof\u202Eexe.txt", "spoofexe.txt")]
    [InlineData("Caf\u00E9 Helper \uFFFD", "Café Helper")]
    [InlineData("", "pid 42")]
    [InlineData(null, "pid 42")]
    public void RemovesControlCharactersAndNamesNamelessProcesses(string? name, string expected) =>
        Assert.Equal(expected, ProcessName.Clean(name, 42));
}
