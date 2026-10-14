using System.Collections.Immutable;
using Vimon.Core;

namespace Vimon.Cli;

/// <summary>Recent samples behind the sparklines. Immutable: every update returns a new history.</summary>
internal sealed record MetricHistory(
    ImmutableArray<double> CpuTempC,
    ImmutableArray<double> CpuLoadPct,
    ImmutableArray<double> GpuTempC,
    ImmutableArray<double> GpuLoadPct,
    ImmutableArray<double> NetDownBps,
    ImmutableArray<double> NetUpBps,
    ImmutableArray<double> DiskReadBps,
    ImmutableArray<double> DiskWriteBps)
{
    /// <summary>20 samples at the 0.5 s refresh interval: the last 10 seconds.</summary>
    public const int Capacity = 20;

    public static readonly MetricHistory Empty = new([], [], [], [], [], [], [], []);

    /// <summary>The scale of each curve, carried across samples so it narrows slowly after a peak.</summary>
    public SparkBands Bands { get; init; } = SparkBands.None;

    public MetricHistory Append(SystemSnapshot snapshot, GpuMetrics? gpu)
    {
        var (down, up) = NetworkTotals.Of(snapshot.Network);
        var (read, write) = DiskTotals.Of(snapshot.Storage);

        var next = new MetricHistory(
            Push(CpuTempC, snapshot.Cpu?.TempC),
            Push(CpuLoadPct, snapshot.Cpu?.LoadPct),
            Push(GpuTempC, gpu?.CoreTempC),
            Push(GpuLoadPct, gpu?.UtilizationPct),
            Push(NetDownBps, down),
            Push(NetUpBps, up),
            Push(DiskReadBps, read),
            Push(DiskWriteBps, write));

        return next with { Bands = BandsFor(next, Bands) };
    }

    /// <summary>Used when the focused GPU changes, so its curves never mix two devices.</summary>
    public MetricHistory WithoutGpu() =>
        this with { GpuTempC = [], GpuLoadPct = [], Bands = Bands with { GpuTempC = null, GpuLoadPct = null } };

    private static SparkBands BandsFor(MetricHistory h, SparkBands previous) => new(
        ScaleBand.Next(previous.CpuTempC, h.CpuTempC),
        ScaleBand.Next(previous.CpuLoadPct, h.CpuLoadPct),
        ScaleBand.Next(previous.GpuTempC, h.GpuTempC),
        ScaleBand.Next(previous.GpuLoadPct, h.GpuLoadPct),
        ScaleBand.Next(previous.NetDownBps, h.NetDownBps),
        ScaleBand.Next(previous.NetUpBps, h.NetUpBps),
        ScaleBand.Next(previous.DiskReadBps, h.DiskReadBps),
        ScaleBand.Next(previous.DiskWriteBps, h.DiskWriteBps));

    private static ImmutableArray<double> Push(ImmutableArray<double> series, double? value)
    {
        if (value is not double v || double.IsNaN(v) || double.IsInfinity(v))
            return series;

        var appended = series.Add(v);
        return appended.Length > Capacity ? appended.RemoveAt(0) : appended;
    }
}
