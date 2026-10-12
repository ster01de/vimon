using Spectre.Console;
using Spectre.Console.Rendering;
using Vimon.Core;

namespace Vimon.Cli;

/// <param name="Width">Console width; from <see cref="Dashboard.SideBySideMinWidth"/> on, panels sit in pairs.</param>
/// <param name="Spinner">Current spinner frame in the live view; null for one-shot output.</param>
/// <param name="Theme">Colours of the view; null means the default theme.</param>
internal sealed record DashboardOptions(
    int Width, bool HideMissing = false, string? Spinner = null, bool IsPaused = false, Theme? Theme = null);

/// <summary>The full view as Spectre.Console widgets. Pure: builds a renderable and never writes.</summary>
internal static class Dashboard
{
    public const int SideBySideMinWidth = 100;
    public const string Missing = "—";

    private const int LabelWidth = 10;
    private const int PairGap = 1;
    private const int ValueWidth = 10;
    private const int BarWidth = 20;
    private const int MaxStatusRows = 3;
    private const int MaxStorageRows = 3;
    private const int MaxNetworkInterfaces = 3;
    private const string FindingDot = "●";

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
        var lower = new IRenderable?[]
            {
                MemoryPanel(s, t, theme),
                NetworkPanel(s, history, options, theme),
                StoragePanel(s, options, theme)
            }
            .OfType<IRenderable>()
            .ToList();

        return new Rows(
            Header(options, theme),
            StatusPanel(diagnostics, theme),
            Pair(CpuPanel(s, history, options, t, theme), GpuPanel(s, gpu, selectedGpu, history, options, t, theme), options.Width),
            PairUp(lower, options.Width));
    }

    private static IRenderable Header(DashboardOptions o, Theme theme)
    {
        string hidden = o.HideMissing ? $"  [{theme.Muted}](empty rows hidden)[/]" : "";

        if (o.Spinner is null)
            return new Markup($"[bold]VIMON[/]{hidden}");

        string state = o.IsPaused
            ? $"[{theme.Warning}]PAUSED[/]"
            : $"[{theme.Good}]{Markup.Escape(o.Spinner)} LIVE[/]";

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
        theme.MarksFindings ? HealthStyle.PaddedToken(level) : FindingDot;

    private static Panel CpuPanel(SystemSnapshot s, MetricHistory h, DashboardOptions o, HealthThresholds t, Theme theme)
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

        return Section("CPU", cpu?.Name ?? "Unknown CPU", MetricsGrid(rows, o.HideMissing, theme), theme);
    }

    private static Panel GpuPanel(
        SystemSnapshot s, GpuMetrics? gpu, int selectedGpu, MetricHistory h, DashboardOptions o, HealthThresholds t, Theme theme)
    {
        string title = s.Gpus.Count == 0 ? "GPU" : $"GPU {selectedGpu + 1}/{s.Gpus.Count}";

        if (gpu is null)
            return Section(title, "No GPU detected", new Text(""), theme);

        var rows = new List<(string Label, string? Value)>
        {
            ("Core", Temperature(gpu.CoreTempC, t.GpuCoreTempC, h.GpuTempC, theme)),
            ("Hotspot", Temperature(gpu.HotspotTempC, t.GpuHotspotTempC, [], theme)),
            ("VRAM temp", Temperature(gpu.MemoryTempC, t.GpuMemoryTempC, [], theme)),
            ("Load", Percent(gpu.UtilizationPct, h.GpuLoadPct, theme)),
            ("Memory", GpuMemory(gpu, theme)),
            ("Power", Watts(gpu.PowerW)),
            ("Fan", Percent(gpu.FanPct, [], theme)),
            ("PCIe", Pcie(gpu, theme))
        };

        return Section(title, gpu.Name, MetricsGrid(rows, o.HideMissing, theme), theme);
    }

    private static Panel MemoryPanel(SystemSnapshot s, HealthThresholds t, Theme theme)
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

        return Section("MEMORY", subtitle: null, MetricsGrid(rows, hideMissing: false, theme), theme);
    }

    /// <returns>Null when hiding missing values and no interface reports a rate.</returns>
    private static Panel? NetworkPanel(SystemSnapshot s, MetricHistory h, DashboardOptions o, Theme theme)
    {
        var (down, up) = NetworkTotals.Of(s.Network);
        var rows = new List<(string Label, string? Value)>
        {
            ("Down", Rate(down, h.NetDownBps, theme)),
            ("Up", Rate(up, h.NetUpBps, theme))
        };

        // Busiest first; the name breaks ties so rows do not swap places while traffic is equal.
        rows.AddRange(s.Network
            .OrderByDescending(rate => (rate.DownBytesPerSec ?? 0) + (rate.UpBytesPerSec ?? 0))
            .ThenBy(rate => rate.Name, StringComparer.Ordinal)
            .Take(MaxNetworkInterfaces)
            .Select(rate => (rate.Name, InterfaceRates(rate))));

        if (o.HideMissing && rows.All(row => row.Value is null))
            return null;

        return Section("NETWORK", subtitle: null, MetricsGrid(rows, o.HideMissing, theme), theme);
    }

    /// <returns>Null when hiding missing values and no drive reports a temperature.</returns>
    private static Panel? StoragePanel(SystemSnapshot s, DashboardOptions o, Theme theme)
    {
        var rows = s.Storage
            .Take(MaxStorageRows)
            .Select(drive => (Label: drive.Name, Value: Celsius(drive.TempC)))
            .ToList();

        if (rows.Count == 0)
            rows.Add(("Temperature", null));

        if (o.HideMissing && rows.All(row => row.Value is null))
            return null;

        // Drive names vary a lot in length (e.g. "Samsung SSD 980 PRO 1TB"), so the label column adapts.
        return Section("STORAGE", subtitle: null, MetricsGrid(rows, o.HideMissing, theme, labelWidth: null), theme);
    }

    private static Panel Section(string title, string? subtitle, IRenderable content, Theme theme)
    {
        IRenderable body = subtitle is null
            ? content
            : new Rows(new Markup($"[bold]{Markup.Escape(subtitle)}[/]"), content);

        return new Panel(body)
        {
            Header = new PanelHeader($" {title} "),
            Border = BoxBorder.Rounded,
            BorderStyle = theme.MutedStyle,
            Expand = true
        };
    }

    /// <param name="labelWidth">Fixed label column so values line up; null sizes it to the longest label.</param>
    private static Grid MetricsGrid(
        IEnumerable<(string Label, string? Value)> rows, bool hideMissing, Theme theme, int? labelWidth = LabelWidth)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn { Width = labelWidth, NoWrap = true });
        grid.AddColumn(new GridColumn());

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

    /// <summary>Panels in pairs from left to right; an odd last panel takes the full width.</summary>
    private static IRenderable PairUp(IReadOnlyList<IRenderable> panels, int width) =>
        new Rows(panels.Chunk(2).Select(pair => pair.Length == 2 ? Pair(pair[0], pair[1], width) : pair[0]));

    /// <summary>Two panels splitting the window evenly, or stacked below <see cref="SideBySideMinWidth"/>.</summary>
    private static IRenderable Pair(IRenderable left, IRenderable right, int width)
    {
        if (width < SideBySideMinWidth)
            return new Rows(left, right);

        int leftWidth = (width - PairGap) / 2;
        int rightWidth = width - PairGap - leftWidth;

        var grid = new Grid();
        grid.AddColumn(new GridColumn { Width = leftWidth, Padding = new Padding(0, 0, PairGap, 0) });
        grid.AddColumn(new GridColumn { Width = rightWidth, Padding = new Padding(0) });
        grid.AddRow(left, right);
        return grid;
    }

    private static string? Temperature(double? value, Threshold threshold, IReadOnlyList<double> history, Theme theme)
    {
        if (!MetricFormat.IsPresent(value) || value <= 0)
            return null;

        string text = $"{value:0.#} °C";
        string color = theme.Of(threshold.Classify(value!.Value));
        return $"[{color}]{text,-ValueWidth}[/]{Spark(Sparkline.ForTemperature(history), theme)}";
    }

    private static string? Percent(double? value, IReadOnlyList<double> history, Theme theme)
    {
        if (!MetricFormat.IsPresent(value))
            return null;

        string text = $"{value:0.#} %";
        return $"{text,-ValueWidth}{Spark(Sparkline.ForLoad(history), theme)}";
    }

    private static string? Rate(double? bytesPerSec, IReadOnlyList<double> history, Theme theme) =>
        MetricFormat.Throughput(bytesPerSec) is string text
            ? $"{text,-ValueWidth}{Spark(Sparkline.ForThroughput(history), theme)}"
            : null;

    private static string? InterfaceRates(InterfaceRate rate)
    {
        string? down = MetricFormat.Throughput(rate.DownBytesPerSec);
        string? up = MetricFormat.Throughput(rate.UpBytesPerSec);

        return down is null && up is null ? null : $"↓{down ?? Missing}  ↑{up ?? Missing}";
    }

    private static string? Celsius(double? value) =>
        MetricFormat.IsPresent(value) && value > 0 ? $"{value:0.#} °C" : null;

    private static string? Watts(double? value) =>
        MetricFormat.IsPresent(value) && value > 0 ? $"{value:0.#} W" : null;

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

    private static string Spark(string blocks, Theme theme) =>
        blocks.Length == 0 ? "" : $" [{theme.Accent}]{blocks}[/]";

    private static string Bar(double percent, string color, Theme theme)
    {
        int filled = MetricFormat.FilledCells(percent, BarWidth);
        return $"[{color}]{new string('█', filled)}[/][{theme.Muted}]{new string('░', BarWidth - filled)}[/]";
    }
}
