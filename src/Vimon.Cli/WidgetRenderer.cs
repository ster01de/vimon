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
/// DISK R12M W110M  ▃▅▇██▇▆▅▃▂
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
    // " 100%" after the process name.
    private const int TopCpuWidth = 5;
    private const int MetricWidth = LabelWidth + 1 + TempWidth + 1 + LoadWidth;
    private const int MetricWithSparkWidth = MetricWidth + SparkGap + SparkLength;
    // ShortThroughput never needs more than four characters ("999K", "1.2M").
    private const int ThroughputWidth = 4;

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
        AddIfPresent(lines, TopProcessLine(s, diagnostics, options.Width));
        AddIfPresent(lines, MetricLine("CPU", s.Cpu?.TempC, t.CpuTempC, s.Cpu?.LoadPct, history.CpuLoadPct, hasSpark, theme));

        if (gpu is not null)
            AddIfPresent(lines, MetricLine("GPU", gpu.CoreTempC, t.GpuCoreTempC, gpu.UtilizationPct, history.GpuLoadPct, hasSpark, theme));

        lines.Add(MemoryLine(s.Memory, t, hasSpark, theme));
        var (down, up) = NetworkTotals.Of(s.Network);
        AddIfPresent(lines, ThroughputLine("NET", ("↓", down), ("↑", up), history.NetDownBps, options.Width, theme));
        var (read, write) = DiskTotals.Of(s.Storage);
        AddIfPresent(lines, ThroughputLine("DISK", ("R", read), ("W", write), history.DiskWriteBps, options.Width, theme));

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
                // Without colour, a token tells warnings and critical findings apart.
                string prefix = theme.MarksFindings ? HealthStyle.PaddedToken(d.Level) + " " : FindingIndent;
                string message = MetricFormat.Truncate(d.Message, width - prefix.Length);
                return $"[{theme.Of(d.Level)}]{prefix}{Markup.Escape(message)}[/]";
            });

    /// <returns>
    /// The busiest process, only while the status is a warning or worse, so a healthy widget does
    /// not flicker between process names.
    /// </returns>
    private static string? TopProcessLine(SystemSnapshot s, IReadOnlyList<Diagnostic> diagnostics, int width)
    {
        if (HealthStyle.Worst(diagnostics) < HealthLevel.Warning)
            return null;

        int nameWidth = width - LabelWidth - TopCpuWidth;
        string? top = MetricFormat.TopProcess(s.TopProcesses, nameWidth);
        return top is null ? null : $"{"TOP",-LabelWidth}{Markup.Escape(top)}";
    }

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

    /// <summary><c>NET ↓1.2M ↑310K</c> or <c>DISK R12M W110M</c>, with a sparkline of <paramref name="history"/>.</summary>
    /// <returns>Null until a rate is known, so the row only appears once there is something to show.</returns>
    private static string? ThroughputLine(
        string label,
        (string Mark, double? BytesPerSec) first,
        (string Mark, double? BytesPerSec) second,
        IReadOnlyList<double> history,
        int width,
        Theme theme)
    {
        string? firstText = MetricFormat.ShortThroughput(first.BytesPerSec);
        string? secondText = MetricFormat.ShortThroughput(second.BytesPerSec);

        if (firstText is null && secondText is null)
            return null;

        // At least one space after the label: "DISK" fills the four-wide label column on its own.
        string values = $"{(label + " ").PadRight(LabelWidth)}{first.Mark}{firstText ?? "",-ThroughputWidth} " +
                        $"{second.Mark}{secondText ?? "",-ThroughputWidth}";
        bool hasSpark = history.Count > 0 && width >= values.Length + SparkGap + SparkLength;

        return hasSpark ? values + Spark(Sparkline.ForThroughput(history.TakeLast(SparkLength).ToList()), theme) : values;
    }

    private static string StateLine(string label, string state, string color, int width, Theme theme)
    {
        string value = MetricFormat.Truncate(state, width - StateLabelWidth);
        return $"[{theme.Muted}]{label,-StateLabelWidth}[/][{color}]{value}[/]";
    }

    private static string Spark(string blocks, Theme theme) =>
        blocks.Length == 0 ? "" : $"{new string(' ', SparkGap)}[{theme.Accent}]{theme.Glyphs.Spark(blocks)}[/]";

    private static string Bar(double percent, string color, Theme theme)
    {
        int filled = MetricFormat.FilledCells(percent, SparkLength);
        return $"[{color}]{new string(theme.Glyphs.BarFull, filled)}[/][{theme.Muted}]{new string(theme.Glyphs.BarEmpty, SparkLength - filled)}[/]";
    }

    private static void AddIfPresent(List<string> lines, string? line)
    {
        if (line is not null)
            lines.Add(line);
    }
}
