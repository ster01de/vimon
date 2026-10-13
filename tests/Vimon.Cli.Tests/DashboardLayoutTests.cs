using System.Collections.Immutable;
using Spectre.Console.Testing;
using Vimon.Core;

namespace Vimon.Cli.Tests;

/// <summary>The live view stands still: rows and columns keep their place while values change.</summary>
public class DashboardLayoutTests
{
    private static readonly MemoryMetrics Memory = new(4, 16, 25, 5, 20);
    private const string Blocks = "▁▂▃▄▅▆▇█";

    private static SystemSnapshot Snapshot() =>
        new(DateTimeOffset.UnixEpoch, new CpuMetrics("Test CPU", 50, 10, 20, 3000), [], Memory, []);

    private static string Render(SystemSnapshot snapshot, int width = 80, MetricHistory? history = null, DashboardOptions? options = null)
    {
        var console = new TestConsole();
        console.Profile.Width = width;
        console.Write(Dashboard.Build(
            snapshot,
            snapshot.Gpus.FirstOrDefault(),
            DiagnosticsEngine.Evaluate(snapshot),
            selectedGpu: 0,
            history ?? MetricHistory.Empty,
            options ?? new DashboardOptions(width)));

        return console.Output;
    }

    private static string[] Lines(string output) =>
        output.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).ToArray();

    private static string LineWith(string output, string text) =>
        Lines(output).First(line => line.Contains(text, StringComparison.Ordinal));

    private static int Count(string line, char c) => line.Count(ch => ch == c);

    [Fact]
    public void ShowsAStillLiveClockInsteadOfASpinner()
    {
        string output = Render(Snapshot(), options: new DashboardOptions(80, Clock: "14:02:11"));

        Assert.Contains("● LIVE 14:02:11", output);
    }

    [Fact]
    public void ShowsPausedInsteadOfTheClock()
    {
        string output = Render(Snapshot(), options: new DashboardOptions(80, Clock: "14:02:11", IsPaused: true));

        Assert.Contains("PAUSED", output);
        Assert.DoesNotContain("LIVE", output);
    }

    [Fact]
    public void KeepsTheKeyHintsInPlaceWhenPaused()
    {
        static int HintColumn(bool paused) =>
            LineWith(Render(Snapshot(), options: new DashboardOptions(80, Clock: "14:02:11", IsPaused: paused)), "VIMON")
                .IndexOf("q quit", StringComparison.Ordinal);

        Assert.Equal(HintColumn(false), HintColumn(true));
    }

    [Fact]
    public void KeepsTheStatusHeightWhenFindingsComeAndGo()
    {
        var calm = Steady();
        var alarming = Steady() with
        {
            Cpu = new CpuMetrics("Hot CPU", 99, 10, null, null),
            Volumes = [new VolumeSpace("Startup disk", "/", "apfs", 1UL << 30, 228UL << 30)],
            Storage = [new StorageMetrics("TOSHIBA External USB 3.0 with a very long name indeed", null) { IsExternal = true, UsbLink = UsbSpeed.High }]
        };

        Assert.Equal(Lines(Render(calm)).Length, Lines(Render(alarming)).Length);
    }

    [Fact]
    public void ShowsTheFreeSpaceOfLargeVolumesInFull()
    {
        var snapshot = Steady() with { Volumes = [new VolumeSpace("Big", "/Volumes/Big", "exfat", 1900UL << 30, 4000UL << 30)] };

        Assert.Contains($"{1900.0:0.0} GB free", LineWith(Render(snapshot), "Big"));
    }

    [Fact]
    public void EndsBothPanelsOfAPairOnTheSameLine()
    {
        // CPU (5 rows) beside GPU (one line), MEMORY (3) beside PROCESSES (5), NETWORK (3) beside DISK (6).
        var corners = Lines(Render(Busy(Steady(), 1), width: 120))
            .Where(line => line.TrimStart().StartsWith('╰'))
            .ToList();

        // The first bottom border is the full-width STATUS panel.
        Assert.Equal(4, corners.Count);
        Assert.All(corners.Skip(1), line => Assert.Equal(2, Count(line, '╰')));
    }

    [Fact]
    public void StacksPanelsWithoutPaddingInNarrowWindows()
    {
        string output = Render(Steady(), width: 80);

        Assert.All(Lines(output), line => Assert.True(Count(line, '╰') <= 1, line));
    }

    [Theory]
    [InlineData(80)]
    [InlineData(100)]
    [InlineData(120)]
    public void KeepsTheFrameHeightFromTheFirstSampleToFullTraffic(int width)
    {
        var first = Render(Steady(), width);
        var someTraffic = Render(Busy(Steady(), 1_000), width, History(1));
        var fullTraffic = Render(Busy(Steady(), 50_000_000), width, History(MetricHistory.Capacity));

        Assert.Equal(Lines(first).Length, Lines(someTraffic).Length);
        Assert.Equal(Lines(first).Length, Lines(fullTraffic).Length);
    }

    [Fact]
    public void KeepsValueColumnsInPlaceWhenTheUnitChanges()
    {
        string slow = LineWith(Render(Busy(Steady(), 556)), "Down");
        string fast = LineWith(Render(Busy(Steady(), 7_800_000)), "Down");

        Assert.Equal(slow.IndexOf("/s", StringComparison.Ordinal), fast.IndexOf("/s", StringComparison.Ordinal));
    }

    [Fact]
    public void KeepsDriveColumnsInPlaceFromBytesToMegabytes()
    {
        static int WriteColumn(double rate) =>
            LineWith(Render(Busy(Steady(), rate)), "SSD").IndexOf(" W ", StringComparison.Ordinal);

        Assert.Equal(WriteColumn(5), WriteColumn(123_000_000));
    }

    [Fact]
    public void ShowsIdleForADriveWithoutTraffic()
    {
        string drive = LineWith(Render(Busy(Steady(), 0)), "SSD");

        Assert.Contains("idle", drive);
        Assert.DoesNotContain("B/s", drive);
    }

    [Fact]
    public void KeepsTheBusiestRowWhileNothingReadsOrWrites()
    {
        Assert.Contains(Dashboard.Missing, LineWith(Render(Steady()), "Busiest"));
        Assert.DoesNotContain("Busiest", Render(Steady() with { ProcessScope = ProcessScope.None }));
    }

    [Fact]
    public void CutsLongBusiestProcessNamesSoTheRowNeverWraps()
    {
        var snapshot = Busy(Steady(), 1_000) with
        {
            TopDiskProcesses = [new ProcessUsage(1, "A very long process name", 1, 0) { DiskReadBytesPerSec = 1, DiskWriteBytesPerSec = 1 }]
        };

        var lines = Lines(Render(snapshot, width: Dashboard.SideBySideMinWidth));
        int busiest = Array.FindIndex(lines, line => line.Contains("Busiest", StringComparison.Ordinal));

        Assert.Contains("A very long…", lines[busiest]);
        Assert.Contains(" W ", lines[busiest]);
        Assert.Contains("Startup", lines[busiest + 1]);
    }

    [Fact]
    public void DrawsSparklinesAtTheirFullLengthFromTheFirstSample()
    {
        static int CurveEnd(MetricHistory history) =>
            LineWith(Render(Snapshot(), history: history), "Temp").LastIndexOfAny(Blocks.ToCharArray());

        var one = MetricHistory.Empty with { CpuTempC = [40] };
        var full = MetricHistory.Empty with
        {
            CpuTempC = Enumerable.Range(0, MetricHistory.Capacity).Select(i => 40.0 + i).ToImmutableArray()
        };

        Assert.Equal(CurveEnd(full), CurveEnd(one));
    }

    // A Mac-like machine at rest: one drive, a startup volume, one interface, process sampling not yet done.
    private static SystemSnapshot Steady() => Snapshot() with
    {
        Storage = [new StorageMetrics("APPLE SSD", null) { Interconnect = "Apple Fabric", IsExternal = false }],
        Volumes = [new VolumeSpace("Startup disk", "/", "apfs", 100UL << 30, 228UL << 30)],
        Network = [new InterfaceRate("en0", null, null)],
        ProcessScope = ProcessScope.CurrentUser
    };

    private static SystemSnapshot Busy(SystemSnapshot s, double rate) => s with
    {
        Storage = [s.Storage[0] with { ReadBytesPerSec = rate, WriteBytesPerSec = rate }],
        Network = [new InterfaceRate("en0", rate, rate)],
        TopProcesses = Enumerable.Range(1, ProcessesPanel.MaxRows).Select(i => new ProcessUsage(i, $"proc{i}", i, 1UL << 20)).ToList(),
        TopDiskProcesses = [new ProcessUsage(1, "Finder", 1, 0) { DiskReadBytesPerSec = rate, DiskWriteBytesPerSec = rate }]
    };

    private static MetricHistory History(int samples)
    {
        var values = Enumerable.Range(0, samples).Select(i => 40.0 + i).ToImmutableArray();
        return new MetricHistory(values, values, values, values, values, values, values, values);
    }
}
