using Spectre.Console;
using Spectre.Console.Rendering;
using Vimon.Core;

namespace Vimon.Cli;

/// <param name="Width">Every line stays within this many columns.</param>
/// <param name="Timestamp">Shown at the right of the status line as a sign of life; null for one-shot output.</param>
/// <param name="Theme">Colours of the widget; null means the default theme.</param>
internal sealed record WidgetOptions(int Width, string? Timestamp = null, Theme? Theme = null);

/// <summary>
/// Borderless, read-only view for desktop panels such as kitty's <c>kitten panel</c>:
/// <code>
/// WARN                  14:02:11
///   OS memory pressure elevated
/// CPU   47°C  33%  ▂▃▅▆▇▆▅▄▃▂
/// RAM         81%  ████████░░
/// NET ↓1.2M ↑310K  ▁▁▂▅█▆▃▁▁▁
/// </code>
/// Only foreground colours are used, so a transparent terminal background shows through, and
/// missing values are left out. Pure: builds a renderable and never writes.
/// </summary>
internal static class WidgetRenderer
{
    public const int MaxWidth = 30;
    public const int MinWidth = 16;
    public const int SparkLength = 10;

    private const int MaxFindings = 2;
    private const string FindingIndent = "  ";
    private const int LabelWidth = 4;
    private const int TempWidth = 5;
    private const int LoadWidth = 4;
    private const int StateLabelWidth = 10;
    private const int SparkGap = 2;
    private const int MetricWidth = LabelWidth + 1 + TempWidth + 1 + LoadWidth;
    private const int MetricWithSparkWidth = MetricWidth + SparkGap + SparkLength;
    // ShortThroughput never needs more than four characters ("999K", "1.2M").
    private const int ThroughputWidth = 4;
    // "NET ↓1.2M ↑310K" is as wide as a metric row, so the sparklines line up.
    private const int NetworkWidth = LabelWidth + (1 + ThroughputWidth) + 1 + (1 + ThroughputWidth);
    private const int NetworkWithSparkWidth = NetworkWidth + SparkGap + SparkLength;

    public static IRenderable Build(
        SystemSnapshot s,
        GpuMetrics? gpu,
        IReadOnlyList<Diagnostic> diagnostics,
        MetricHistory history,
        WidgetOptions options,
        HealthThresholds? thresholds = null)
    {
        var t = thresholds ?? HealthThresholds.Default;
        var theme = options.Theme ?? Theme.Default;
        bool hasSpark = options.Width >= MetricWithSparkWidth;

        var lines = new List<string> { StatusLine(diagnostics, options, theme) };
        lines.AddRange(FindingLines(diagnostics, options.Width, theme));
        AddIfPresent(lines, MetricLine("CPU", s.Cpu?.TempC, t.CpuTempC, s.Cpu?.LoadPct, history.CpuLoadPct, hasSpark, theme));

        if (gpu is not null)
            AddIfPresent(lines, MetricLine("GPU", gpu.CoreTempC, t.GpuCoreTempC, gpu.UtilizationPct, history.GpuLoadPct, hasSpark, theme));

        lines.Add(MemoryLine(s.Memory, t, hasSpark, theme));
        AddIfPresent(lines, NetworkLine(s.Network, history.NetDownBps, options.Width >= NetworkWithSparkWidth, theme));

        if (s.Memory.Pressure is MemoryPressure pressure)
            lines.Add(StateLine("Pressure", pressure.ToString(), theme.Of(HealthStyle.LevelOf(pressure)), options.Width, theme));

        if (s.Thermal is ThermalState thermal)
            lines.Add(StateLine("Thermal", thermal.ToString(), theme.Of(HealthStyle.LevelOf(thermal)), options.Width, theme));

        return new Rows(lines.Select(line => new Markup(line)));
    }

    private static string StatusLine(IReadOnlyList<Diagnostic> diagnostics, WidgetOptions options, Theme theme)
    {
        var worst = HealthStyle.Worst(diagnostics);
        string token = HealthStyle.Token(worst);
        string status = $"[{theme.Of(worst)}]{token}[/]";

        if (options.Timestamp is not string time || token.Length + 1 + time.Length > options.Width)
            return status;

        string gap = new(' ', options.Width - token.Length - time.Length);
        return $"{status}{gap}[{theme.Muted}]{Markup.Escape(time)}[/]";
    }

    // Worst first; OrderByDescending is stable, so equal levels keep the engine's order.
    private static IEnumerable<string> FindingLines(IReadOnlyList<Diagnostic> diagnostics, int width, Theme theme) =>
        diagnostics
            .Where(d => d.Level != HealthLevel.Good)
            .OrderByDescending(d => d.Level)
            .Take(MaxFindings)
            .Select(d =>
            {
                string message = MetricFormat.Truncate(d.Message, width - FindingIndent.Length);
                return $"{FindingIndent}[{theme.Of(d.Level)}]{Markup.Escape(message)}[/]";
            });

    /// <returns>Null when neither temperature nor load is known, so the row disappears.</returns>
    private static string? MetricLine(
        string label, double? tempC, Threshold threshold, double? loadPct, IReadOnlyList<double> loadHistory, bool hasSpark, Theme theme)
    {
        string? temp = MetricFormat.ShortTemperature(tempC);
        string? load = MetricFormat.ShortPercent(loadPct);

        if (temp is null && load is null)
            return null;

        string tempCell = temp is null
            ? new string(' ', TempWidth)
            : $"[{theme.Of(threshold.Classify(tempC!.Value))}]{temp,TempWidth}[/]";
        string spark = hasSpark && load is not null
            ? Spark(Sparkline.ForLoad(loadHistory.TakeLast(SparkLength).ToList()), theme)
            : "";

        return $"{label,-LabelWidth} {tempCell} {load ?? "",LoadWidth}{spark}";
    }

    private static string MemoryLine(MemoryMetrics m, HealthThresholds t, bool hasBar, Theme theme)
    {
        string color = theme.Of(t.RamLoadPct.Classify(m.LoadPct));
        string load = MetricFormat.ShortPercent(m.LoadPct) ?? "";
        string bar = hasBar ? new string(' ', SparkGap) + Bar(m.LoadPct, color, theme) : "";

        return $"{"RAM",-LabelWidth} {new string(' ', TempWidth)} [{color}]{load,LoadWidth}[/]{bar}";
    }

    /// <returns>Null until a rate is known, so the row only appears once there is something to show.</returns>
    private static string? NetworkLine(
        IReadOnlyList<InterfaceRate> network, IReadOnlyList<double> downHistory, bool hasSpark, Theme theme)
    {
        var (down, up) = NetworkTotals.Of(network);
        string? downText = MetricFormat.ShortThroughput(down);
        string? upText = MetricFormat.ShortThroughput(up);

        if (downText is null && upText is null)
            return null;

        string spark = hasSpark && downText is not null
            ? Spark(Sparkline.ForThroughput(downHistory.TakeLast(SparkLength).ToList()), theme)
            : "";

        return $"{"NET",-LabelWidth}↓{downText ?? "",-ThroughputWidth} ↑{upText ?? "",-ThroughputWidth}{spark}";
    }

    private static string StateLine(string label, string state, string color, int width, Theme theme)
    {
        string value = MetricFormat.Truncate(state, width - StateLabelWidth);
        return $"[{theme.Muted}]{label,-StateLabelWidth}[/][{color}]{value}[/]";
    }

    private static string Spark(string blocks, Theme theme) =>
        blocks.Length == 0 ? "" : $"{new string(' ', SparkGap)}[{theme.Accent}]{blocks}[/]";

    private static string Bar(double percent, string color, Theme theme)
    {
        int filled = MetricFormat.FilledCells(percent, SparkLength);
        return $"[{color}]{new string('█', filled)}[/][{theme.Muted}]{new string('░', SparkLength - filled)}[/]";
    }

    private static void AddIfPresent(List<string> lines, string? line)
    {
        if (line is not null)
            lines.Add(line);
    }
}
