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
    ImmutableArray<double> NetUpBps)
{
    /// <summary>20 samples at the 0.5 s refresh interval: the last 10 seconds.</summary>
    public const int Capacity = 20;

    public static readonly MetricHistory Empty = new([], [], [], [], [], []);

    public MetricHistory Append(SystemSnapshot snapshot, GpuMetrics? gpu)
    {
        var (down, up) = NetworkTotals.Of(snapshot.Network);

        return new(
            Push(CpuTempC, snapshot.Cpu?.TempC),
            Push(CpuLoadPct, snapshot.Cpu?.LoadPct),
            Push(GpuTempC, gpu?.CoreTempC),
            Push(GpuLoadPct, gpu?.UtilizationPct),
            Push(NetDownBps, down),
            Push(NetUpBps, up));
    }

    /// <summary>Used when the focused GPU changes, so its curves never mix two devices.</summary>
    public MetricHistory WithoutGpu() => this with { GpuTempC = [], GpuLoadPct = [] };

    private static ImmutableArray<double> Push(ImmutableArray<double> series, double? value)
    {
        if (value is not double v || double.IsNaN(v) || double.IsInfinity(v))
            return series;

        var appended = series.Add(v);
        return appended.Length > Capacity ? appended.RemoveAt(0) : appended;
    }
}
