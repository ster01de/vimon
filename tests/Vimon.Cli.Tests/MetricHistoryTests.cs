using Vimon.Core;

namespace Vimon.Cli.Tests;

public class MetricHistoryTests
{
    private static readonly MemoryMetrics Memory = new(4, 16, 25, 5, 20);

    private static SystemSnapshot Snapshot(double? cpuTempC, double? cpuLoadPct = 10) =>
        new(DateTimeOffset.UnixEpoch, new CpuMetrics("Test CPU", cpuTempC, cpuLoadPct, null, null), [], Memory, []);

    private static GpuMetrics Gpu(double coreTempC) =>
        new(0, "Test GPU", Vendor.Nvidia,
            coreTempC, null, null, 50, null, null, null, null, null, null,
            null, null, null, null);

    [Fact]
    public void AppendsPresentValuesAndSkipsMissingOnes()
    {
        var history = MetricHistory.Empty
            .Append(Snapshot(cpuTempC: 40), Gpu(55))
            .Append(Snapshot(cpuTempC: null, cpuLoadPct: null), gpu: null);

        Assert.Equal(new[] { 40.0 }, history.CpuTempC);
        Assert.Equal(new[] { 10.0 }, history.CpuLoadPct);
        Assert.Equal(new[] { 55.0 }, history.GpuTempC);
    }

    [Fact]
    public void AppendsTheTotalNetworkRates()
    {
        var snapshot = Snapshot(cpuTempC: 40) with { Network = [new("en0", 1000, 10), new("en7", 500, null)] };

        var history = MetricHistory.Empty.Append(snapshot, gpu: null);

        Assert.Equal(new[] { 1500.0 }, history.NetDownBps);
        Assert.Equal(new[] { 10.0 }, history.NetUpBps);
    }

    [Fact]
    public void KeepsOnlyTheMostRecentSamples()
    {
        var history = MetricHistory.Empty;

        for (int i = 0; i < MetricHistory.Capacity + 5; i++)
            history = history.Append(Snapshot(cpuTempC: i), gpu: null);

        Assert.Equal(MetricHistory.Capacity, history.CpuTempC.Length);
        Assert.Equal(5.0, history.CpuTempC[0]);
    }

    [Fact]
    public void DoesNotChangeTheOriginalWhenAppending()
    {
        var original = MetricHistory.Empty.Append(Snapshot(cpuTempC: 40), gpu: null);

        _ = original.Append(Snapshot(cpuTempC: 50), gpu: null);

        Assert.Equal(new[] { 40.0 }, original.CpuTempC);
    }

    [Fact]
    public void ClearsOnlyGpuSeriesWhenTheGpuChanges()
    {
        var history = MetricHistory.Empty.Append(Snapshot(cpuTempC: 40), Gpu(55)).WithoutGpu();

        Assert.Empty(history.GpuTempC);
        Assert.Empty(history.GpuLoadPct);
        Assert.Equal(new[] { 40.0 }, history.CpuTempC);
    }
}
