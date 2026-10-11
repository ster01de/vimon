namespace Vimon.Cli;

/// <summary>Plain-text formatting shared by the compact and widget views. Pure.</summary>
internal static class MetricFormat
{
    private const string Ellipsis = "…";

    public static bool IsPresent(double? value) =>
        value is double v && !double.IsNaN(v) && !double.IsInfinity(v);

    /// <returns>Whole degrees such as <c>47°C</c>, or null when the sensor reports nothing usable.</returns>
    public static string? ShortTemperature(double? value) =>
        IsPresent(value) && value > 0 ? $"{value:0}°C" : null;

    /// <returns>Whole percent such as <c>33%</c>, or null when missing.</returns>
    public static string? ShortPercent(double? value) =>
        IsPresent(value) ? $"{value:0}%" : null;

    /// <summary>Cuts <paramref name="text"/> to <paramref name="width"/>, ending in an ellipsis when cut.</summary>
    public static string Truncate(string text, int width)
    {
        if (text.Length <= width)
            return text;

        return width <= 0 ? "" : text[..(width - 1)] + Ellipsis;
    }

    /// <summary>Cuts or pads to exactly <paramref name="width"/>; a width of 0 or less leaves the line as it is.</summary>
    public static string Fit(string line, int width) =>
        width <= 0 ? line : Truncate(line, width).PadRight(width);
}
