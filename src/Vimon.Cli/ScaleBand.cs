namespace Vimon.Cli;

/// <summary>
/// The value range a sparkline is scaled to. It widens at once when a new peak arrives, but narrows
/// only gradually once the peak has left the window, so the whole curve does not jump. Immutable.
/// </summary>
internal readonly record struct ScaleBand(double Low, double High)
{
    /// <summary>Share of the gap kept per sample while narrowing: about 3 s to settle at 0.5 s per sample.</summary>
    public const double ReleasePerSample = 0.85;

    /// <returns>The band for <paramref name="window"/>, or null when the window is empty.</returns>
    public static ScaleBand? Next(ScaleBand? previous, IReadOnlyList<double> window)
    {
        if (window.Count == 0)
            return null;

        double low = window.Min();
        double high = window.Max();

        if (previous is not ScaleBand before)
            return new ScaleBand(low, high);

        return new ScaleBand(
            low <= before.Low ? low : low - (low - before.Low) * ReleasePerSample,
            high >= before.High ? high : high + (before.High - high) * ReleasePerSample);
    }

    /// <summary>The band widened to cover <paramref name="values"/>, so no value ever falls outside it.</summary>
    public ScaleBand Covering(IReadOnlyList<double> values) =>
        values.Count == 0 ? this : new ScaleBand(Math.Min(Low, values.Min()), Math.Max(High, values.Max()));
}

/// <summary>The scale band of every sparkline series in <see cref="MetricHistory"/>; null while a series is empty.</summary>
internal sealed record SparkBands(
    ScaleBand? CpuTempC,
    ScaleBand? CpuLoadPct,
    ScaleBand? GpuTempC,
    ScaleBand? GpuLoadPct,
    ScaleBand? NetDownBps,
    ScaleBand? NetUpBps,
    ScaleBand? DiskReadBps,
    ScaleBand? DiskWriteBps)
{
    public static readonly SparkBands None = new(null, null, null, null, null, null, null, null);
}
