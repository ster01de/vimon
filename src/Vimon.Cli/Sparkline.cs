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

    public static string ForTemperature(IReadOnlyList<double> values) => Fitted(values, TempMinSpanC, TempMaxC);

    public static string ForLoad(IReadOnlyList<double> values) => Fitted(values, LoadMinSpanPct, LoadMaxPct);

    public static string ForThroughput(IReadOnlyList<double> values) => Fitted(values, ThroughputMinSpanBps, double.MaxValue);

    /// <summary>Values outside <paramref name="min"/>..<paramref name="max"/> are clamped to the edges.</summary>
    public static string Render(IReadOnlyList<double> values, double min, double max)
    {
        if (values.Count == 0 || max <= min)
            return "";

        return new string(values.Select(value => Blocks[Level(value, min, max)]).ToArray());
    }

    /// <summary>
    /// Fits the scale to the values so small real changes stay visible, but never narrower than
    /// <paramref name="minSpan"/> (so sensor noise is not blown up) and never outside the bounds.
    /// </summary>
    public static (double Min, double Max) Scale(
        IReadOnlyList<double> values, double minSpan, double lowerBound, double upperBound)
    {
        if (values.Count == 0)
            return (lowerBound, upperBound);

        double low = values.Min();
        double high = values.Max();
        double span = Math.Max(high - low, minSpan);
        double center = (low + high) / 2;

        double min = Math.Max(lowerBound, center - span / 2);
        double max = Math.Min(upperBound, min + span);
        return (Math.Max(lowerBound, max - span), max);
    }

    private static string Fitted(IReadOnlyList<double> values, double minSpan, double upperBound)
    {
        var (min, max) = Scale(values, minSpan, lowerBound: 0, upperBound);
        return Render(values, min, max);
    }

    private static int Level(double value, double min, double max)
    {
        double ratio = (Math.Clamp(value, min, max) - min) / (max - min);
        return (int)Math.Round(ratio * (Blocks.Length - 1), MidpointRounding.AwayFromZero);
    }
}
