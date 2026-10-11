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
        SystemSnapshot snapshot, int width = 80, bool hideMissing = false, MetricHistory? history = null)
    {
        var console = new TestConsole();
        console.Profile.Width = width;
        console.Write(Dashboard.Build(
            snapshot,
            snapshot.Gpus.FirstOrDefault(),
            DiagnosticsEngine.Evaluate(snapshot),
            selectedGpu: 0,
            history ?? MetricHistory.Empty,
            new DashboardOptions(width, hideMissing)));

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
    public void ShowsMissingValuesAsDashByDefault()
    {
        string output = Render(Snapshot(gpus: Gpu()));

        Assert.Contains(Dashboard.Missing, LineWith(output, "Hotspot"));
        Assert.Contains("STORAGE", output);
    }

    [Fact]
    public void HidesRowsAndPanelsWithoutValueWhenRequested()
    {
        string output = Render(Snapshot(gpus: Gpu()), hideMissing: true);

        Assert.DoesNotContain(Dashboard.Missing, output);
        Assert.DoesNotContain("Hotspot", output);
        Assert.DoesNotContain("PCIe", output);
        Assert.DoesNotContain("STORAGE", output);
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
    public void ShowsGpuMemoryUsedAndTotal()
    {
        string output = Render(Snapshot(gpus: Gpu(memoryUsedGb: 1.5, memoryTotalGb: 24)));

        Assert.Contains($"{1.5:0.0} / {24.0:0.0} GB", LineWith(output, "Memory"));
    }
}
