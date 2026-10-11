namespace Vimon.Cli;

/// <summary>Renders a series as block characters, e.g. <c>▁▂▄▆█</c>. Pure.</summary>
internal static class Sparkline
{
    private const string Blocks = "▁▂▃▄▅▆▇█";

    /// <summary>Values outside <paramref name="min"/>..<paramref name="max"/> are clamped to the edges.</summary>
    public static string Render(IReadOnlyList<double> values, double min, double max)
    {
        if (values.Count == 0 || max <= min)
            return "";

        return new string(values.Select(value => Blocks[Level(value, min, max)]).ToArray());
    }

    private static int Level(double value, double min, double max)
    {
        double ratio = (Math.Clamp(value, min, max) - min) / (max - min);
        return (int)Math.Round(ratio * (Blocks.Length - 1), MidpointRounding.AwayFromZero);
    }
}
