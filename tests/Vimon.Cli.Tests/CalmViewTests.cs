using Spectre.Console.Testing;
using Vimon.Core;

namespace Vimon.Cli.Tests;

/// <summary>The dashboard shows the calmed sample: smooth rates, idle on time, a steady busiest process.</summary>
public class CalmViewTests
{
    private static readonly MemoryMetrics Memory = new(4, 16, 25, 5, 20);

    [Fact]
    public void ShowsIdleOnceTrafficStops()
    {
        var shown = Calm(Copying(0, 1_000_000), Copying(0.5, 0), Copying(1.0, 0));

        Assert.Contains("idle", LineWith(Render(shown), "SSD"));
    }

    [Fact]
    public void KeepsTheBusiestNameAcrossCloseReadings()
    {
        var first = Copying(0, 1_000_000) with { TopDiskProcesses = [Disk(1, "Finder", 100_000), Disk(2, "cp", 90_000)] };
        var second = Copying(0.5, 1_000_000) with { TopDiskProcesses = [Disk(2, "cp", 120_000), Disk(1, "Finder", 100_000)] };

        Assert.Contains("Finder", LineWith(Render(Calm(first, second)), "Busiest"));
    }

    [Fact]
    public void ShowsTheRawProcessesOnTheFirstSample()
    {
        var sample = Copying(0, 0) with { TopProcesses = [new ProcessUsage(7, "Xcode", 50, 1UL << 30)] };

        Assert.Equal(7, Calm(sample).TopProcesses[0].Pid);
    }

    [Fact]
    public void StartsTheRowsOverAfterAPause()
    {
        // The backend lists the busiest first: process 1 leads, then process 2 is slightly ahead.
        var before = Copying(0, 0) with { TopProcesses = [new ProcessUsage(1, "a", 30, 0), new ProcessUsage(2, "b", 28, 0)] };
        var after = new[] { new ProcessUsage(2, "b", 30, 0), new ProcessUsage(1, "a", 28, 0) };

        Assert.Equal(1, Calm(before, Copying(0.5, 0) with { TopProcesses = after }).TopProcesses[0].Pid);
        Assert.Equal(2, Calm(before, Copying(60, 0) with { TopProcesses = after }).TopProcesses[0].Pid);
    }

    private static SystemSnapshot Calm(params SystemSnapshot[] samples)
    {
        var state = CalmView.Empty;
        SystemSnapshot shown = null!;

        foreach (var sample in samples)
            (shown, state) = state.Apply(sample);

        return shown;
    }

    private static SystemSnapshot Copying(double seconds, double rate) =>
        new SystemSnapshot(
            DateTimeOffset.UnixEpoch.AddSeconds(seconds),
            new CpuMetrics("Test CPU", 50, 10, null, null),
            [],
            Memory,
            [new StorageMetrics("APPLE SSD", null) { ReadBytesPerSec = rate, WriteBytesPerSec = rate, IsExternal = false }])
        {
            ProcessScope = ProcessScope.CurrentUser
        };

    private static ProcessUsage Disk(int pid, string name, double bytesPerSec) =>
        new ProcessUsage(pid, name, 1, 0) { DiskReadBytesPerSec = 0, DiskWriteBytesPerSec = bytesPerSec };

    private static string Render(SystemSnapshot snapshot)
    {
        var console = new TestConsole();
        console.Profile.Width = 80;
        console.Write(Dashboard.Build(
            snapshot, null, DiagnosticsEngine.Evaluate(snapshot), selectedGpu: 0, MetricHistory.Empty, new DashboardOptions(80)));
        return console.Output;
    }

    private static string LineWith(string output, string text) =>
        output.Split('\n').First(line => line.Contains(text, StringComparison.Ordinal));
}
