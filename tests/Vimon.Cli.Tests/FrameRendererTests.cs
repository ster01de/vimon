using Vimon.Core;

namespace Vimon.Cli.Tests;

public class FrameRendererTests
{
    private const int Width = 60;

    private static readonly MemoryMetrics Memory = new(4, 16, 25, 5, 20);

    private static SystemSnapshot Snapshot(params GpuMetrics[] gpus) =>
        new(DateTimeOffset.UnixEpoch, new CpuMetrics("Test CPU", 50, 10, 20, 3000), gpus, Memory, []);

    private static GpuMetrics Gpu(double? memoryUsedGb, double? memoryTotalGb) =>
        new(0, "Test GPU", Vendor.Nvidia,
            60, null, null, 30, 100, 40, null, null,
            memoryUsedGb, memoryTotalGb,
            null, null, null, null);

    private static string Render(SystemSnapshot snapshot) =>
        FrameRenderer.Render(
            snapshot,
            snapshot.Gpus.FirstOrDefault(),
            DiagnosticsEngine.Evaluate(snapshot),
            selectedGpu: 0,
            Width,
            "-");

    private static string RowValue(string frame, string label) =>
        frame.Split(Environment.NewLine)
            .Select(line => line.Trim('|').Trim())
            .First(line => line.StartsWith(label + " ", StringComparison.Ordinal))[label.Length..]
            .Trim();

    [Fact]
    public void ShowsGpuMemoryUsedAndTotal()
    {
        string frame = Render(Snapshot(Gpu(memoryUsedGb: 1.5, memoryTotalGb: 24)));

        Assert.Equal($"{1.5:0.0} / {24.0:0.0} GB", RowValue(frame, "Memory"));
    }

    [Fact]
    public void ShowsGpuMemoryUsedWithoutTotal()
    {
        string frame = Render(Snapshot(Gpu(memoryUsedGb: 1.5, memoryTotalGb: null)));

        Assert.Equal($"{1.5:0.0} GB", RowValue(frame, "Memory"));
    }

    [Fact]
    public void ShowsDashWhenGpuMemoryIsUnknown()
    {
        string frame = Render(Snapshot(Gpu(memoryUsedGb: null, memoryTotalGb: 24)));

        Assert.Equal("—", RowValue(frame, "Memory"));
    }

    [Fact]
    public void ShowsThermalAndPressureOnlyWhenReported()
    {
        var without = Snapshot();
        var with = without with
        {
            Thermal = ThermalState.Fair,
            Memory = Memory with { Pressure = MemoryPressure.Normal }
        };

        string plain = Render(without);
        string extended = Render(with);

        Assert.DoesNotContain("Thermal", plain);
        Assert.DoesNotContain("Pressure", plain);
        Assert.Equal("Fair", RowValue(extended, "Thermal"));
        Assert.Equal("Normal", RowValue(extended, "Pressure"));
    }

    [Fact]
    public void ShowsStatusDirectlyBelowTheHeader()
    {
        var lines = Render(Snapshot()).Split(Environment.NewLine)
            .Select(line => line.Trim('|').Trim())
            .ToList();

        int status = lines.IndexOf("STATUS");
        int cpu = lines.IndexOf("CPU");

        // Header is border, title, key hints, border: STATUS must be the first section after it,
        // so truncation in short terminals never hides the health verdict.
        Assert.Equal(4, status);
        Assert.True(cpu > status);
    }

    [Fact]
    public void KeepsEveryLineAtTheRequestedWidth()
    {
        string frame = Render(Snapshot(Gpu(memoryUsedGb: 1.5, memoryTotalGb: 24)));

        var lines = frame.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.All(lines, line => Assert.Equal(Width, line.Length));
    }
}
