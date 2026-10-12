using Vimon.Core;

namespace Vimon.Cli;

/// <summary>Plain-text formatting shared by the compact and widget views. Pure.</summary>
internal static class MetricFormat
{
    private const string Ellipsis = "…";
    // Decimal units like Activity Monitor and Task Manager's network graphs.
    private const double UnitStep = 1000;
    // 999.5 would round to "1000", so it already moves up to the next unit.
    private const double NextUnitAt = 999.5;
    private static readonly string[] ByteUnits = ["B", "K", "M", "G"];
    // Memory in binary units, like the RAM row.
    private const double Mebibyte = 1024 * 1024;
    private const double Gibibyte = Mebibyte * 1024;

    public static bool IsPresent(double? value) =>
        value is double v && !double.IsNaN(v) && !double.IsInfinity(v);

    /// <returns>Whole degrees such as <c>47°C</c>, or null when the sensor reports nothing usable.</returns>
    public static string? ShortTemperature(double? value) =>
        IsPresent(value) && value > 0 ? $"{value:0}°C" : null;

    /// <returns>Whole percent such as <c>33%</c>, or null when missing.</returns>
    public static string? ShortPercent(double? value) =>
        IsPresent(value) ? $"{value:0}%" : null;

    /// <returns>Bytes per second such as <c>512 B/s</c>, <c>310 KB/s</c> or <c>1.2 MB/s</c>; null when missing.</returns>
    public static string? Throughput(double? bytesPerSec)
    {
        if (!IsPresent(bytesPerSec) || bytesPerSec < 0)
            return null;

        var (value, unit) = ScaleBytes(bytesPerSec!.Value);
        return unit == "B" ? $"{value:0} B/s" : $"{Number(value)} {unit}B/s";
    }

    /// <returns>Bytes per second in at most four characters such as <c>512B</c>, <c>310K</c> or <c>1.2M</c>; null when missing.</returns>
    public static string? ShortThroughput(double? bytesPerSec)
    {
        if (!IsPresent(bytesPerSec) || bytesPerSec < 0)
            return null;

        var (value, unit) = ScaleBytes(bytesPerSec!.Value);
        return unit == "B" ? $"{value:0}B" : $"{Number(value)}{unit}";
    }

    /// <returns>Memory such as <c>512 MB</c> or <c>4.1 GB</c>.</returns>
    public static string Memory(ulong bytes) =>
        bytes >= Gibibyte ? $"{bytes / Gibibyte:0.0} GB" : $"{bytes / Mebibyte:0} MB";

    /// <returns>The top process as <c>Xcode 23%</c>, or null without a CPU value.</returns>
    public static string? TopProcess(IReadOnlyList<ProcessUsage> processes, int nameWidth) =>
        processes.Count > 0 && processes[0].CpuPct is double pct
            ? $"{Truncate(processes[0].Name, nameWidth)} {pct:0}%"
            : null;

    /// <summary>Filled cells of a bar with <paramref name="cells"/> cells; 0 for a missing or non-finite value.</summary>
    public static int FilledCells(double percent, int cells) =>
        double.IsFinite(percent)
            ? (int)Math.Round(Math.Clamp(percent, 0, 100) / 100 * cells, MidpointRounding.AwayFromZero)
            : 0;

    private static (double Value, string Unit) ScaleBytes(double bytes)
    {
        int unit = 0;

        while (bytes >= NextUnitAt && unit < ByteUnits.Length - 1)
        {
            bytes /= UnitStep;
            unit++;
        }

        return (bytes, ByteUnits[unit]);
    }

    // One decimal while the number is small, so 1.2 MB/s does not show as 1 MB/s.
    private static string Number(double value) => value < 9.95 ? $"{value:0.0}" : $"{value:0}";

    /// <summary>Cuts <paramref name="text"/> to <paramref name="width"/>, ending in an ellipsis when cut.</summary>
    public static string Truncate(string text, int width)
    {
        if (text.Length <= width)
            return text;

        if (width <= 0)
            return "";

        // Never keep half of a surrogate pair (e.g. an emoji) in front of the ellipsis.
        int keep = width - 1;
        if (keep > 0 && char.IsHighSurrogate(text[keep - 1]))
            keep--;

        return text[..keep] + Ellipsis;
    }

    /// <summary>Cuts or pads to exactly <paramref name="width"/>; a width of 0 or less leaves the line as it is.</summary>
    public static string Fit(string line, int width) =>
        width <= 0 ? line : Truncate(line, width).PadRight(width);
}
