namespace Vimon.Cli;

/// <summary>Renders a series as block characters, e.g. <c>▁▂▄▆█</c>. Pure.</summary>
internal static class Sparkline
{
    private const string Blocks = "▁▂▃▄▅▆▇█";
    // The scale fits the recent values, but spans at least this much so sensor noise stays flat.
    private const double TempMinSpanC = 10;
    private const double TempMaxC = 130;
    private const double LoadMinSpanPct = 20;
    private const double LoadMaxPct = 100;
    // 64 KB/s: background chatter on an idle link stays a flat line.
    private const double ThroughputMinSpanBps = 64_000;

    // A band (see ScaleBand) keeps the scale from jumping when a peak leaves the window; without one,
    // the scale fits the values alone.
    public static string ForTemperature(IReadOnlyList<double> values, ScaleBand? band = null) =>
        Fitted(values, band, TempMinSpanC, TempMaxC);

    public static string ForLoad(IReadOnlyList<double> values, ScaleBand? band = null) =>
        Fitted(values, band, LoadMinSpanPct, LoadMaxPct);

    public static string ForThroughput(IReadOnlyList<double> values, ScaleBand? band = null) =>
        Fitted(values, band, ThroughputMinSpanBps, double.MaxValue);

    /// <summary>Values outside <paramref name="min"/>..<paramref name="max"/> are clamped to the edges.</summary>
    public static string Render(IReadOnlyList<double> values, double min, double max)
    {
        if (values.Count == 0 || max <= min)
            return "";

        return new string(values.Select(value => Blocks[Level(value, min, max)]).ToArray());
    }

    /// <summary>
    /// The newest <paramref name="length"/> blocks, padded with blanks on the left, so a curve has its
    /// full length from the first sample and new values always enter at the same place.
    /// </summary>
    public static string Fixed(string blocks, int length) =>
        (blocks.Length > length ? blocks[^length..] : blocks).PadLeft(length);

    /// <summary>
    /// Fits the scale to the values so small real changes stay visible, but never narrower than
    /// <paramref name="minSpan"/> (so sensor noise is not blown up) and never outside the bounds.
    /// </summary>
    public static (double Min, double Max) Scale(
        IReadOnlyList<double> values, double minSpan, double lowerBound, double upperBound) =>
        values.Count == 0
            ? (lowerBound, upperBound)
            : Scale(new ScaleBand(values.Min(), values.Max()), minSpan, lowerBound, upperBound);

    /// <summary>Like <see cref="Scale(IReadOnlyList{double}, double, double, double)"/>, for a known value range.</summary>
    public static (double Min, double Max) Scale(ScaleBand range, double minSpan, double lowerBound, double upperBound)
    {
        double low = range.Low;
        double high = range.High;
        double span = Math.Max(high - low, minSpan);
        double center = (low + high) / 2;

        double min = Math.Max(lowerBound, center - span / 2);
        double max = Math.Min(upperBound, min + span);
        return (Math.Max(lowerBound, max - span), max);
    }

    private static string Fitted(IReadOnlyList<double> values, ScaleBand? band, double minSpan, double upperBound)
    {
        var (min, max) = band is ScaleBand known && values.Count > 0
            ? Scale(known.Covering(values), minSpan, lowerBound: 0, upperBound)
            : Scale(values, minSpan, lowerBound: 0, upperBound);
        return Render(values, min, max);
    }

    private static int Level(double value, double min, double max)
    {
        double ratio = (Math.Clamp(value, min, max) - min) / (max - min);
        return (int)Math.Round(ratio * (Blocks.Length - 1), MidpointRounding.AwayFromZero);
    }
}
