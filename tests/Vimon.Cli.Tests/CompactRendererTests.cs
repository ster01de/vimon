using Vimon.Core;

namespace Vimon.Cli.Tests;

public class CompactRendererTests
{
    private static readonly MemoryMetrics Memory = new(4, 16, 25, 5, 20);

    private static SystemSnapshot Snapshot(CpuMetrics? cpu = null, params GpuMetrics[] gpus) =>
        new(DateTimeOffset.UnixEpoch, cpu ?? Cpu(50, 10), gpus, Memory, []);

    private static CpuMetrics Cpu(double? tempC, double? loadPct) => new("Test CPU", tempC, loadPct, null, null);

    private static GpuMetrics Gpu(double? coreTempC, double? loadPct) =>
        new(0, "Test GPU", Vendor.Nvidia,
            coreTempC, null, null, loadPct, null, null, null, null, null, null,
            null, null, null, null);

    private static string[] Render(SystemSnapshot snapshot, int selectedGpu = 0, int? maxWidth = null) =>
        CompactRenderer.Render(
                snapshot,
                snapshot.Gpus.Count > 0 ? snapshot.Gpus[selectedGpu] : null,
                DiagnosticsEngine.Evaluate(snapshot),
                selectedGpu,
                maxWidth)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void RendersOneLineWhenHealthy()
    {
        var lines = Render(Snapshot(gpus: Gpu(60, 30)));

        Assert.Equal(new[] { "OK │ CPU 50°C 10% │ GPU 60°C 30% │ RAM 25%" }, lines);
    }

    [Fact]
    public void LeavesOutMissingValuesAndEmptySegments()
    {
        var lines = Render(Snapshot(Cpu(tempC: null, loadPct: 10)));

        Assert.Equal(new[] { "OK │ CPU 10% │ RAM 25%" }, lines);
    }

    [Fact]
    public void AddsDiagnosticsLineWithWorstLevelFirst()
    {
        var snapshot = Snapshot(Cpu(tempC: 95, loadPct: 10)) with { Thermal = ThermalState.Serious };

        var lines = Render(snapshot);

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("CRIT │ CPU 95°C 10%", lines[0]);
        Assert.Equal("  CPU 95°C · System thermal state serious, throttling likely", lines[1]);
    }

    [Fact]
    public void AddsNetworkThroughputWhenKnown()
    {
        var lines = Render(Snapshot() with { Network = [new("en0", 1_200_000, 310_000)] });

        Assert.Equal(new[] { $"OK │ CPU 50°C 10% │ RAM 25% │ NET ↓{1.2:0.0}M ↑310K" }, lines);
    }

    [Fact]
    public void NamesTheBusiestProcessOnlyWhenSomethingIsWrong()
    {
        ProcessUsage[] top = [new(1, "Xcode", 23.4, 1)];
        var healthy = Snapshot() with { TopProcesses = top };
        var hot = Snapshot(Cpu(tempC: 95, loadPct: 10)) with { TopProcesses = top };

        Assert.Single(Render(healthy));
        Assert.Equal("  CPU 95°C · top Xcode 23%", Render(hot)[1]);
    }

    [Fact]
    public void NumbersTheSelectedGpuWhenThereAreSeveral()
    {
        var lines = Render(Snapshot(null, Gpu(60, 30), Gpu(70, 90)), selectedGpu: 1);

        Assert.Contains("GPU 2/2 70°C 90%", lines[0]);
    }

    [Fact]
    public void FitsEveryLineExactlyToTheGivenWidth()
    {
        var snapshot = Snapshot(Cpu(tempC: 95, loadPct: 10), Gpu(60, 30));

        var lines = Render(snapshot, maxWidth: 30);

        Assert.All(lines, line => Assert.Equal(30, line.Length));
        Assert.EndsWith("…", lines[0]);
    }

    [Fact]
    public void PadsShortLinesSoTheLiveViewOverwritesOldCharacters()
    {
        var lines = Render(Snapshot(), maxWidth: 60);

        Assert.Equal(60, lines[0].Length);
        Assert.Equal("OK │ CPU 50°C 10% │ RAM 25%", lines[0].TrimEnd());
    }
}
