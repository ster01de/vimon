using Spectre.Console;
using Spectre.Console.Rendering;
using Vimon.Core;

namespace Vimon.Cli;

/// <param name="Width">Console width; from <see cref="Dashboard.SideBySideMinWidth"/> on, panels sit in pairs.</param>
/// <param name="Spinner">Current spinner frame in the live view; null for one-shot output.</param>
internal sealed record DashboardOptions(int Width, bool HideMissing = false, string? Spinner = null, bool IsPaused = false);

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
        var memory = MemoryPanel(s, t);
        var storage = StoragePanel(s, options);

        return new Rows(
            Header(options),
            StatusPanel(diagnostics),
            Pair(CpuPanel(s, history, options, t), GpuPanel(s, gpu, selectedGpu, history, options, t), options.Width),
            storage is null ? memory : Pair(memory, storage, options.Width));
    }

    private static IRenderable Header(DashboardOptions o)
    {
        if (o.Spinner is null)
            return new Markup(o.HideMissing ? "[bold]VIMON[/]  [grey](empty rows hidden)[/]" : "[bold]VIMON[/]");

        string state = o.IsPaused ? "[yellow]PAUSED[/]" : $"[green]{Markup.Escape(o.Spinner)} LIVE[/]";
        string hidden = o.HideMissing ? "  [grey](empty rows hidden)[/]" : "";

        return new Markup(
            $"[bold]VIMON[/]  {state}{hidden}   [grey]q quit · p pause · h hide {Missing} · c compact · ←/→ GPU[/]");
    }

    private static IRenderable StatusPanel(IReadOnlyList<Diagnostic> diagnostics)
    {
        var worst = HealthStyle.Worst(diagnostics);
        var lines = diagnostics
            .Take(MaxStatusRows)
            .Select(d => new Markup($"[{HealthStyle.ColorName(d.Level)}]●[/] {Markup.Escape(d.Message)}"));

        return new Panel(new Rows(lines))
        {
            Header = new PanelHeader(" STATUS "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(HealthStyle.ColorOf(worst)),
            Expand = true
        };
    }

    private static Panel CpuPanel(SystemSnapshot s, MetricHistory h, DashboardOptions o, HealthThresholds t)
    {
        var cpu = s.Cpu;
        var rows = new List<(string Label, string? Value)>
        {
            ("Temp", Temperature(cpu?.TempC, t.CpuTempC, h.CpuTempC)),
            ("Load", Percent(cpu?.LoadPct, h.CpuLoadPct)),
            ("Power", Watts(cpu?.PackagePowerW))
        };

        if (s.Thermal is ThermalState thermal)
            rows.Add(("Thermal", $"[{HealthStyle.ThermalColor(thermal)}]{thermal}[/]"));

        return Section("CPU", cpu?.Name ?? "Unknown CPU", MetricsGrid(rows, o.HideMissing));
    }

    private static Panel GpuPanel(
        SystemSnapshot s, GpuMetrics? gpu, int selectedGpu, MetricHistory h, DashboardOptions o, HealthThresholds t)
    {
        string title = s.Gpus.Count == 0 ? "GPU" : $"GPU {selectedGpu + 1}/{s.Gpus.Count}";

        if (gpu is null)
            return Section(title, "No GPU detected", new Text(""));

        var rows = new List<(string Label, string? Value)>
        {
            ("Core", Temperature(gpu.CoreTempC, t.GpuCoreTempC, h.GpuTempC)),
            ("Hotspot", Temperature(gpu.HotspotTempC, t.GpuHotspotTempC, [])),
            ("VRAM temp", Temperature(gpu.MemoryTempC, t.GpuMemoryTempC, [])),
            ("Load", Percent(gpu.UtilizationPct, h.GpuLoadPct)),
            ("Memory", GpuMemory(gpu)),
            ("Power", Watts(gpu.PowerW)),
            ("Fan", Percent(gpu.FanPct, [])),
            ("PCIe", Pcie(gpu))
        };

        return Section(title, gpu.Name, MetricsGrid(rows, o.HideMissing));
    }

    private static Panel MemoryPanel(SystemSnapshot s, HealthThresholds t)
    {
        var m = s.Memory;
        string color = HealthStyle.ColorName(t.RamLoadPct.Classify(m.LoadPct));
        var rows = new List<(string Label, string? Value)>
        {
            ("RAM", $"{m.UsedGb:0.0} / {m.TotalGb:0.0} GB"),
            ("Usage", $"{Bar(m.LoadPct, color)} [{color}]{m.LoadPct:0} %[/]")
        };

        if (m.Pressure is MemoryPressure pressure)
            rows.Add(("Pressure", $"[{HealthStyle.PressureColor(pressure)}]{pressure}[/]"));

        return Section("MEMORY", subtitle: null, MetricsGrid(rows, hideMissing: false));
    }

    /// <returns>Null when hiding missing values and no drive reports a temperature.</returns>
    private static Panel? StoragePanel(SystemSnapshot s, DashboardOptions o)
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
        return Section("STORAGE", subtitle: null, MetricsGrid(rows, o.HideMissing, labelWidth: null));
    }

    private static Panel Section(string title, string? subtitle, IRenderable content)
    {
        IRenderable body = subtitle is null
            ? content
            : new Rows(new Markup($"[bold]{Markup.Escape(subtitle)}[/]"), content);

        return new Panel(body)
        {
            Header = new PanelHeader($" {title} "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Expand = true
        };
    }

    /// <param name="labelWidth">Fixed label column so values line up; null sizes it to the longest label.</param>
    private static Grid MetricsGrid(
        IEnumerable<(string Label, string? Value)> rows, bool hideMissing, int? labelWidth = LabelWidth)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn { Width = labelWidth, NoWrap = true });
        grid.AddColumn(new GridColumn());

        foreach (var (label, value) in rows)
        {
            if (value is null && hideMissing)
                continue;

            grid.AddRow(
                new Markup($"[grey]{Markup.Escape(label)}[/]"),
                new Markup(value ?? $"[grey]{Missing}[/]"));
        }

        return grid;
    }

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

    private static string? Temperature(double? value, Threshold threshold, IReadOnlyList<double> history)
    {
        if (!MetricFormat.IsPresent(value) || value <= 0)
            return null;

        string text = $"{value:0.#} °C";
        return $"[{HealthStyle.ColorName(threshold.Classify(value!.Value))}]{text,-ValueWidth}[/]{Spark(Sparkline.ForTemperature(history))}";
    }

    private static string? Percent(double? value, IReadOnlyList<double> history)
    {
        if (!MetricFormat.IsPresent(value))
            return null;

        string text = $"{value:0.#} %";
        return $"{text,-ValueWidth}{Spark(Sparkline.ForLoad(history))}";
    }

    private static string? Celsius(double? value) =>
        MetricFormat.IsPresent(value) && value > 0 ? $"{value:0.#} °C" : null;

    private static string? Watts(double? value) =>
        MetricFormat.IsPresent(value) && value > 0 ? $"{value:0.#} W" : null;

    private static string? GpuMemory(GpuMetrics gpu)
    {
        if (gpu.MemoryUsedGb is not double used || double.IsNaN(used) || used < 0)
            return null;

        if (gpu.MemoryTotalGb is not double total || total <= 0)
            return $"{used:0.0} GB";

        return $"{Bar(used / total * 100, "steelblue")} {used:0.0} / {total:0.0} GB";
    }

    private static string? Pcie(GpuMetrics gpu)
    {
        if (!gpu.PcieGenerationCurrent.HasValue || !gpu.PcieWidthCurrent.HasValue)
            return null;

        string current = $"Gen{gpu.PcieGenerationCurrent} x{gpu.PcieWidthCurrent}";

        return gpu.PcieGenerationMax.HasValue && gpu.PcieWidthMax.HasValue
            ? $"{current} [grey]/ max Gen{gpu.PcieGenerationMax} x{gpu.PcieWidthMax}[/]"
            : current;
    }

    private static string Spark(string blocks) =>
        blocks.Length == 0 ? "" : $" [steelblue]{blocks}[/]";

    private static string Bar(double percent, string color)
    {
        int filled = MetricFormat.FilledCells(percent, BarWidth);
        return $"[{color}]{new string('█', filled)}[/][grey]{new string('░', BarWidth - filled)}[/]";
    }
}
