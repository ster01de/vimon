using Vimon.Core;

namespace Vimon.Cli.Tests;

/// <summary>A sparkline's scale widens at once for a peak and narrows slowly after it.</summary>
public class ScaleBandTests
{
    private static readonly MemoryMetrics Memory = new(4, 16, 25, 5, 20);

    [Fact]
    public void IsNullForAnEmptyWindow() => Assert.Null(ScaleBand.Next(new ScaleBand(0, 10), []));

    [Fact]
    public void StartsAtTheRangeOfTheWindow() => Assert.Equal(new ScaleBand(2, 5), ScaleBand.Next(null, [5, 2, 3]));

    [Fact]
    public void WidensAtOnceForANewPeak() => Assert.Equal(50, ScaleBand.Next(new ScaleBand(0, 10), [0, 50])!.Value.High);

    [Fact]
    public void NarrowsByTheReleaseFactorPerSample()
    {
        var band = ScaleBand.Next(new ScaleBand(0, 100), [0, 10])!.Value;

        Assert.Equal(10 + 90 * ScaleBand.ReleasePerSample, band.High, precision: 9);
        Assert.Equal(0, band.Low);
    }

    [Fact]
    public void RaisesTheLowEdgeSlowly()
    {
        var band = ScaleBand.Next(new ScaleBand(0, 10), [8, 10])!.Value;

        Assert.Equal(8 - 8 * ScaleBand.ReleasePerSample, band.Low, precision: 9);
    }

    [Fact]
    public void SparklineUsesTheBandInsteadOfTheWindow()
    {
        double[] values = [10, 20];

        Assert.Equal("▂▂", Sparkline.ForLoad(values, new ScaleBand(0, 100)));
        Assert.Equal("▃▆", Sparkline.ForLoad(values));
    }

    [Fact]
    public void SparklineStillWidensANarrowBandToTheMinimumSpan()
    {
        double[] values = [50, 50];

        Assert.Equal(Sparkline.ForLoad(values), Sparkline.ForLoad(values, new ScaleBand(50, 50)));
    }

    [Fact]
    public void SparklineNeverClipsAValueOutsideTheBand() =>
        Assert.Equal("▁█", Sparkline.ForLoad([0, 100], new ScaleBand(40, 60)));

    [Fact]
    public void HistoryKeepsTheScaleWideAfterASpikeLeavesTheWindow()
    {
        var history = MetricHistory.Empty.Append(Downloading(1_000_000), gpu: null);

        for (int i = 0; i < MetricHistory.Capacity; i++)
            history = history.Append(Downloading(1_000), gpu: null);

        Assert.Equal(1_000, history.NetDownBps.Max());
        Assert.True(history.Bands.NetDownBps!.Value.High > 100_000);
    }

    [Fact]
    public void HistoryForgetsTheGpuScaleWhenTheGpuChanges()
    {
        var gpu = new GpuMetrics(0, "Test GPU", Vendor.Nvidia, 60, null, null, 50, null, null, null, null, null, null, null, null, null, null);

        var history = MetricHistory.Empty.Append(Downloading(1_000), gpu).WithoutGpu();

        Assert.Null(history.Bands.GpuTempC);
        Assert.Null(history.Bands.GpuLoadPct);
        Assert.NotNull(history.Bands.NetDownBps);
    }

    private static SystemSnapshot Downloading(double bytesPerSec) =>
        new SystemSnapshot(DateTimeOffset.UnixEpoch, null, [], Memory, []) { Network = [new InterfaceRate("en0", bytesPerSec, 0)] };
}
