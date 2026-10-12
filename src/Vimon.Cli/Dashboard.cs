using Spectre.Console;
using Spectre.Console.Rendering;
using Vimon.Core;

namespace Vimon.Cli;

/// <param name="Width">Console width; from <see cref="Dashboard.SideBySideMinWidth"/> on, panels sit in pairs.</param>
/// <param name="Clock">Time of the shown sample in the live view, e.g. <c>14:02:11</c>; null for one-shot output.</param>
/// <param name="Theme">Colours of the view; null means the default theme.</param>
internal sealed record DashboardOptions(
    int Width, bool HideMissing = false, string? Clock = null, bool IsPaused = false, Theme? Theme = null);

/// <summary>The full view as Spectre.Console widgets. Pure: builds a renderable and never writes.</summary>
internal static class Dashboard
{
    public const int SideBySideMinWidth = PanelLayout.SideBySideMinWidth;
    public const string Missing = "—";

    // Columns are sized so every row fits a side-by-side panel at 100 columns without wrapping.
    internal const int LabelWidth = 10;
    private const int ValueWidth = 8;
    private const int SparkLength = MetricHistory.Capacity;
    private const int BarWidth = 20;
    private const int MaxStatusRows = 3;
    private const int MaxNetworkInterfaces = 3;

    public static IRenderable Build(
        SystemSnapshot s,
        GpuMetrics? gpu,
        IReadOnlyList<Diagnostic> diagnostics,
        int selectedGpu,
        MetricHistory history,
        DashboardOptions options,
        HealthThresholds? thresholds = null)
    {
        var t = thresholds ?? HealthThresholds.Default;
        var theme = options.Theme ?? Theme.Default;
        var lower = new Section?[]
            {
                MemoryPanel(s, t, theme),
                ProcessesSection(s, options, theme),
                NetworkPanel(s, history, options, theme),
                DiskSection(s, history, options, t, theme)
            }
            .OfType<Section>()
            .ToList();

        return new Rows(
            Header(options, theme),
            StatusPanel(diagnostics, theme),
            PanelLayout.Pair(CpuPanel(s, history, options, t, theme), GpuPanel(s, gpu, selectedGpu, history, options, t, theme), options.Width),
            PanelLayout.PairUp(lower, options.Width));
    }

    private static IRenderable Header(DashboardOptions o, Theme theme)
    {
        string hidden = o.HideMissing ? $"  [{theme.Muted}](empty rows hidden)[/]" : "";

        if (o.Clock is null)
            return new Markup($"[bold]VIMON[/]{hidden}");

        // A still clock instead of a spinner: the header only changes when there is a new sample.
        string state = o.IsPaused
            ? $"[{theme.Warning}]PAUSED[/]"
            : $"[{theme.Good}]{theme.Glyphs.Bullet} LIVE {Markup.Escape(o.Clock)}[/]";

        return new Markup(
            $"[bold]VIMON[/]  {state}{hidden}   [{theme.Muted}]q quit · p pause · h hide {Missing} · c compact · ←/→ GPU[/]");
    }

    /// <remarks>The worst token sits in the header, so the status reads without colour too.</remarks>
    private static IRenderable StatusPanel(IReadOnlyList<Diagnostic> diagnostics, Theme theme)
    {
        var worst = HealthStyle.Worst(diagnostics);
        var lines = diagnostics
            .Take(MaxStatusRows)
            .Select(d => new Markup($"[{theme.Of(d.Level)}]{FindingMarker(d.Level, theme)}[/] {Markup.Escape(d.Message)}"));

        return new Panel(new Rows(lines))
        {
            Header = new PanelHeader($" STATUS · {HealthStyle.Token(worst)} "),
            Border = BoxBorder.Rounded,
            BorderStyle = theme.StyleOf(worst),
            Expand = true
        };
    }

    private static string FindingMarker(HealthLevel level, Theme theme) =>
        theme.MarksFindings ? HealthStyle.PaddedToken(level) : theme.Glyphs.Bullet;

    private static Section CpuPanel(SystemSnapshot s, MetricHistory h, DashboardOptions o, HealthThresholds t, Theme theme)
    {
        var cpu = s.Cpu;
        var rows = new List<(string Label, string? Value)>
        {
            ("Temp", Temperature(cpu?.TempC, t.CpuTempC, h.CpuTempC, theme)),
            ("Load", Percent(cpu?.LoadPct, h.CpuLoadPct, theme)),
            ("Power", Watts(cpu?.PackagePowerW))
        };

        if (s.Thermal is ThermalState thermal)
            rows.Add(("Thermal", $"[{theme.Of(HealthStyle.LevelOf(thermal))}]{thermal}[/]"));

        return Titled("CPU", cpu?.Name ?? "Unknown CPU", MetricsGrid(rows, o.HideMissing, theme), theme);
    }

    private static Section GpuPanel(
        SystemSnapshot s, GpuMetrics? gpu, int selectedGpu, MetricHistory h, DashboardOptions o, HealthThresholds t, Theme theme)
    {
        string title = s.Gpus.Count == 0 ? "GPU" : $"GPU {selectedGpu + 1}/{s.Gpus.Count}";

        if (gpu is null)
            return Titled(title, "No GPU detected", new Text(""), theme);

        var rows = new List<(string Label, string? Value)>
        {
            ("Core", Temperature(gpu.CoreTempC, t.GpuCoreTempC, h.GpuTempC, theme)),
            ("Hotspot", Temperature(gpu.HotspotTempC, t.GpuHotspotTempC, null, theme)),
            ("VRAM temp", Temperature(gpu.MemoryTempC, t.GpuMemoryTempC, null, theme)),
            ("Load", Percent(gpu.UtilizationPct, h.GpuLoadPct, theme)),
            ("Memory", GpuMemory(gpu, theme)),
            ("Power", Watts(gpu.PowerW)),
            ("Fan", Percent(gpu.FanPct, null, theme)),
            ("PCIe", Pcie(gpu, theme))
        };

        return Titled(title, gpu.Name, MetricsGrid(rows, o.HideMissing, theme), theme);
    }

    private static Section MemoryPanel(SystemSnapshot s, HealthThresholds t, Theme theme)
    {
        var m = s.Memory;
        string color = theme.Of(t.RamLoadPct.Classify(m.LoadPct));
        var rows = new List<(string Label, string? Value)>
        {
            ("RAM", $"{m.UsedGb:0.0} / {m.TotalGb:0.0} GB"),
            ("Usage", $"{Bar(m.LoadPct, color, theme)} [{color}]{m.LoadPct:0} %[/]")
        };

        if (m.Pressure is MemoryPressure pressure)
            rows.Add(("Pressure", $"[{theme.Of(HealthStyle.LevelOf(pressure))}]{pressure}[/]"));

        return Titled("MEMORY", subtitle: null, MetricsGrid(rows, hideMissing: false, theme), theme);
    }

    /// <returns>Null when the backend reads no processes, or when hiding an empty list.</returns>
    private static Section? ProcessesSection(SystemSnapshot s, DashboardOptions o, Theme theme) =>
        ProcessesPanel.Title(s) is string title && ProcessesPanel.Body(s, o.HideMissing, theme) is IRenderable body
            ? Titled(title, subtitle: null, body, theme)
            : null;

    /// <returns>Null when hiding missing values and no interface reports a rate.</returns>
    private static Section? NetworkPanel(SystemSnapshot s, MetricHistory h, DashboardOptions o, Theme theme)
    {
        var (down, up) = NetworkTotals.Of(s.Network);
        var rows = new List<(string Label, string? Value)>
        {
            ("Down", Rate(down, h.NetDownBps, theme)),
            ("Up", Rate(up, h.NetUpBps, theme))
        };

        // By name, not by traffic, so rows keep their place while the traffic shifts.
        rows.AddRange(s.Network
            .OrderBy(rate => rate.Name, StringComparer.Ordinal)
            .Take(MaxNetworkInterfaces)
            .Select(rate => (rate.Name, InterfaceRates(rate))));

        if (o.HideMissing && rows.All(row => row.Value is null))
            return null;

        return Titled("NETWORK", subtitle: null, MetricsGrid(rows, o.HideMissing, theme), theme);
    }

    /// <returns>Null when hiding missing values and there is neither a volume nor a drive temperature.</returns>
    private static Section? DiskSection(SystemSnapshot s, MetricHistory h, DashboardOptions o, HealthThresholds t, Theme theme)
    {
        var rows = DiskPanel.Rows(s, h, t, theme);

        if (rows.Count == 0)
            rows.Add(("Volumes", null));

        if (o.HideMissing && rows.All(row => row.Value is null))
            return null;

        return Titled("DISK", subtitle: null, MetricsGrid(rows, o.HideMissing, theme), theme);
    }

    private static Section Titled(string title, string? subtitle, IRenderable content, Theme theme)
    {
        IRenderable body = subtitle is null
            ? content
            : new Rows(new Markup($"[bold]{Markup.Escape(subtitle)}[/]"), content);

        return new Section(title, body, theme.MutedStyle);
    }

    /// <summary>Label and value columns of fixed width, so values line up across panels.</summary>
    private static Grid MetricsGrid(IEnumerable<(string Label, string? Value)> rows, bool hideMissing, Theme theme)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn { Width = LabelWidth, NoWrap = true, Padding = new Padding(0, 0, 1, 0) });
        grid.AddColumn(new GridColumn { NoWrap = true, Padding = new Padding(0) });

        foreach (var (label, value) in rows)
        {
            if (value is null && hideMissing)
                continue;

            grid.AddRow(
                new Markup($"[{theme.Muted}]{Markup.Escape(label)}[/]"),
                new Markup(value ?? $"[{theme.Muted}]{Missing}[/]"));
        }

        return grid;
    }

    // Values are right-aligned in a fixed column; a null history means the row has no curve.
    private static string? Temperature(double? value, Threshold threshold, IReadOnlyList<double>? history, Theme theme)
    {
        if (!MetricFormat.IsPresent(value) || value <= 0)
            return null;

        string text = MetricFormat.AlignRight($"{value:0.#} °C", ValueWidth);
        string color = theme.Of(threshold.Classify(value!.Value));
        return $"[{color}]{text}[/]{Spark(history, Sparkline.ForTemperature, theme)}";
    }

    private static string? Percent(double? value, IReadOnlyList<double>? history, Theme theme) =>
        MetricFormat.IsPresent(value)
            ? $"{MetricFormat.AlignRight($"{value:0.#} %", ValueWidth)}{Spark(history, Sparkline.ForLoad, theme)}"
            : null;

    /// <summary>A throughput row with its curve; shared with the DISK panel.</summary>
    internal static string? Rate(double? bytesPerSec, IReadOnlyList<double> history, Theme theme) =>
        MetricFormat.Throughput(bytesPerSec) is string text
            ? $"{MetricFormat.AlignRight(text, ValueWidth)}{Spark(history, Sparkline.ForThroughput, theme)}"
            : null;

    private static string? InterfaceRates(InterfaceRate rate)
    {
        string? down = MetricFormat.Throughput(rate.DownBytesPerSec);
        string? up = MetricFormat.Throughput(rate.UpBytesPerSec);

        return down is null && up is null
            ? null
            : $"↓{MetricFormat.AlignRight(down ?? Missing, ValueWidth)}  ↑{MetricFormat.AlignRight(up ?? Missing, ValueWidth)}";
    }

    private static string? Watts(double? value) =>
        MetricFormat.IsPresent(value) && value > 0 ? MetricFormat.AlignRight($"{value:0.#} W", ValueWidth) : null;

    private static string? GpuMemory(GpuMetrics gpu, Theme theme)
    {
        if (gpu.MemoryUsedGb is not double used || double.IsNaN(used) || used < 0)
            return null;

        if (gpu.MemoryTotalGb is not double total || total <= 0)
            return $"{used:0.0} GB";

        return $"{Bar(used / total * 100, theme.Accent, theme)} {used:0.0} / {total:0.0} GB";
    }

    private static string? Pcie(GpuMetrics gpu, Theme theme)
    {
        if (!gpu.PcieGenerationCurrent.HasValue || !gpu.PcieWidthCurrent.HasValue)
            return null;

        string current = $"Gen{gpu.PcieGenerationCurrent} x{gpu.PcieWidthCurrent}";

        return gpu.PcieGenerationMax.HasValue && gpu.PcieWidthMax.HasValue
            ? $"{current} [{theme.Muted}]/ max Gen{gpu.PcieGenerationMax} x{gpu.PcieWidthMax}[/]"
            : current;
    }

    /// <summary>A curve of fixed length, blank until samples arrive, so the row never grows.</summary>
    private static string Spark(IReadOnlyList<double>? history, Func<IReadOnlyList<double>, string> render, Theme theme) =>
        history is null
            ? ""
            : $" [{theme.Accent}]{theme.Glyphs.Spark(Sparkline.Fixed(render(history), SparkLength))}[/]";

    private static string Bar(double percent, string color, Theme theme)
    {
        int filled = MetricFormat.FilledCells(percent, BarWidth);
        return $"[{color}]{new string(theme.Glyphs.BarFull, filled)}[/][{theme.Muted}]{new string(theme.Glyphs.BarEmpty, BarWidth - filled)}[/]";
    }
}
