using Spectre.Console.Testing;
using Vimon.Core;

namespace Vimon.Cli.Tests;

public class DashboardTests
{
    private static readonly MemoryMetrics Memory = new(4, 16, 25, 5, 20);

    private static SystemSnapshot Snapshot(CpuMetrics? cpu = null, params GpuMetrics[] gpus) =>
        new(DateTimeOffset.UnixEpoch, cpu ?? new CpuMetrics("Test CPU", 50, 10, 20, 3000), gpus, Memory, []);

    // Core temp, load, power and fan are set; hotspot, VRAM temp and PCIe are not.
    private static GpuMetrics Gpu(double? memoryUsedGb = null, double? memoryTotalGb = null) =>
        new(0, "Test GPU", Vendor.Nvidia,
            60, null, null, 30, 100, 40, null, null,
            memoryUsedGb, memoryTotalGb,
            null, null, null, null);

    private static string Render(
        SystemSnapshot snapshot, int width = 80, bool hideMissing = false, MetricHistory? history = null, Theme? theme = null)
    {
        var console = new TestConsole();
        console.Profile.Width = width;
        console.Write(Dashboard.Build(
            snapshot,
            snapshot.Gpus.FirstOrDefault(),
            DiagnosticsEngine.Evaluate(snapshot),
            selectedGpu: 0,
            history ?? MetricHistory.Empty,
            new DashboardOptions(width, hideMissing, Theme: theme)));

        return console.Output;
    }

    private static string[] Lines(string output) =>
        output.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();

    private static string LineWith(string output, string text) =>
        Lines(output).First(line => line.Contains(text, StringComparison.Ordinal));

    [Fact]
    public void ShowsStatusAboveTheMetrics()
    {
        string output = Render(Snapshot(gpus: Gpu()));

        Assert.True(output.IndexOf("STATUS", StringComparison.Ordinal) < output.IndexOf(" CPU ", StringComparison.Ordinal));
        Assert.Contains("No important issues detected", output);
    }

    [Fact]
    public void ListsFindingsInTheStatusPanel()
    {
        string output = Render(Snapshot(new CpuMetrics("Hot CPU", 95, 10, null, null)));

        Assert.Contains("CPU 95°C", output);
    }

    [Fact]
    public void NamesTheWorstLevelInTheStatusHeader()
    {
        string output = Render(Snapshot(new CpuMetrics("Hot CPU", 95, 10, null, null)));

        Assert.Contains("STATUS · CRIT", output);
    }

    [Fact]
    public void MarksFindingsWithTokensInTheMonoTheme()
    {
        string output = Render(Snapshot(new CpuMetrics("Hot CPU", 95, 10, null, null)), theme: Theme.Mono);

        Assert.Contains("CRIT CPU 95°C", LineWith(output, "CPU 95°C"));
        Assert.DoesNotContain("●", output);
    }

    [Fact]
    public void ShowsMissingValuesAsDashByDefault()
    {
        string output = Render(Snapshot(gpus: Gpu()));

        Assert.Contains(Dashboard.Missing, LineWith(output, "Hotspot"));
        Assert.Contains("DISK", output);
    }

    [Fact]
    public void HidesRowsAndPanelsWithoutValueWhenRequested()
    {
        string output = Render(Snapshot(gpus: Gpu()), hideMissing: true);

        Assert.DoesNotContain(Dashboard.Missing, output);
        Assert.DoesNotContain("Hotspot", output);
        Assert.DoesNotContain("PCIe", output);
        Assert.DoesNotContain("DISK", output);
        Assert.Contains("Core", output);
        Assert.Contains("(empty rows hidden)", output);
    }

    [Fact]
    public void PlacesCpuAndGpuSideBySideOnlyInWideWindows()
    {
        static bool HasPairRow(string output) =>
            Lines(output).Any(line => line.Contains(" CPU ", StringComparison.Ordinal)
                                      && line.Contains(" GPU ", StringComparison.Ordinal));

        Assert.True(HasPairRow(Render(Snapshot(gpus: Gpu()), width: Dashboard.SideBySideMinWidth + 20)));
        Assert.False(HasPairRow(Render(Snapshot(gpus: Gpu()), width: Dashboard.SideBySideMinWidth - 20)));
    }

    [Fact]
    public void DrawsSparklineFromHistory()
    {
        var history = MetricHistory.Empty with { CpuTempC = [40, 60, 80] };

        string output = Render(Snapshot(), history: history);

        // The scale fits the recent values (40..80 °C), so the curve uses the full block range.
        Assert.Contains("▁▅█", LineWith(output, "Temp"));
    }

    [Fact]
    public void ShowsNetworkTotalsAndTheBusiestInterfaceFirst()
    {
        var snapshot = Snapshot() with { Network = [new("en7", 2_000, 1_000), new("en0", 1_200_000, 310_000)] };

        string output = Render(snapshot);

        Assert.Contains($"{1.2:0.0} MB/s", LineWith(output, "Down"));
        Assert.Contains("311 KB/s", LineWith(output, "Up"));
        Assert.True(output.IndexOf("en0", StringComparison.Ordinal) < output.IndexOf("en7", StringComparison.Ordinal));
    }

    [Fact]
    public void HidesTheNetworkPanelWithoutRatesOnlyWhenRequested()
    {
        Assert.Contains("NETWORK", Render(Snapshot()));
        Assert.DoesNotContain("NETWORK", Render(Snapshot(), hideMissing: true));
    }

    private static SystemSnapshot WithProcesses(SystemSnapshot snapshot, params ProcessUsage[] processes) =>
        snapshot with { TopProcesses = processes, ProcessScope = ProcessScope.CurrentUser };

    [Fact]
    public void ListsTheBusiestProcessesOfTheUser()
    {
        var snapshot = WithProcesses(
            Snapshot(), new ProcessUsage(1, "Xcode", 23.4, 4UL << 30), new ProcessUsage(2, "Finder", null, 300UL << 20));

        string output = Render(snapshot);

        Assert.Contains("PROCESSES · yours", output);
        Assert.Contains($"{23.4:0.0} %", LineWith(output, "Xcode"));
        Assert.Contains($"{4.0:0.0} GB", LineWith(output, "Xcode"));
        Assert.Contains("300 MB", LineWith(output, "Finder"));
    }

    [Fact]
    public void EscapesAndShortensProcessNames()
    {
        var snapshot = WithProcesses(Snapshot(), new ProcessUsage(1, "[evil] " + new string('x', 40), 1, 1));

        string output = Render(snapshot);

        Assert.Contains("[evil] xxxxxxxxxxxx…", output);
    }

    [Fact]
    public void ShowsNoProcessPanelWhenTheBackendReadsNone()
    {
        Assert.DoesNotContain("PROCESSES", Render(Snapshot()));
        Assert.DoesNotContain("PROCESSES", Render(WithProcesses(Snapshot()), hideMissing: true));
        Assert.Contains("PROCESSES", Render(WithProcesses(Snapshot())));
    }

    [Fact]
    public void ShowsHowFullEachVolumeIsWithTheStartupDiskFirst()
    {
        var snapshot = Snapshot() with
        {
            Volumes =
            [
                new VolumeSpace("TOSHIBA 1TB", "/Volumes/TOSHIBA 1TB", "ntfs", 600UL << 30, 1000UL << 30),
                new VolumeSpace("Startup disk", "/", "apfs", (ulong)(2.7 * (1UL << 30)), 228UL << 30)
            ]
        };

        string output = Render(snapshot);

        // STATUS names the full disk too; the DISK panel comes after it.
        var diskLines = Lines(output).SkipWhile(line => !line.Contains("DISK", StringComparison.Ordinal)).ToList();
        string startup = diskLines.First(line => line.Contains("Startup disk", StringComparison.Ordinal));
        Assert.Contains("99 %", startup);
        Assert.Contains($"{2.7:0.0} GB free", startup);
        Assert.Contains("ntfs", diskLines.First(line => line.Contains("TOSHIBA", StringComparison.Ordinal)));
        Assert.True(diskLines.FindIndex(l => l.Contains("Startup disk")) < diskLines.FindIndex(l => l.Contains("TOSHIBA")));
    }

    [Fact]
    public void ShowsGpuMemoryUsedAndTotal()
    {
        string output = Render(Snapshot(gpus: Gpu(memoryUsedGb: 1.5, memoryTotalGb: 24)));

        Assert.Contains($"{1.5:0.0} / {24.0:0.0} GB", LineWith(output, "Memory"));
    }
}
