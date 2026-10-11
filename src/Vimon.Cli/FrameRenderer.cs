using System.Text;
using Vimon.Core;

namespace Vimon.Cli;

/// <summary>Builds the text frame for one snapshot. Pure: no console access.</summary>
internal static class FrameRenderer
{
    public const int MinWidth = 42;
    public const int MaxWidth = 96;
    private const int MaxStorageRows = 3;
    private const int MaxStatusRows = 3;

    public static string Render(
        SystemSnapshot s,
        GpuMetrics? gpu,
        IReadOnlyList<Diagnostic> diagnostics,
        int selectedGpu,
        int width,
        string spin)
    {
        int w = Math.Clamp(width, MinWidth, MaxWidth);
        var b = new StringBuilder();

        void Border() =>
            b.Append('+').Append(new string('-', w - 2)).AppendLine("+");

        void Row(string text = "")
        {
            string t = " " + text;
            int inner = w - 2;

            if (t.Length > inner)
                t = t[..inner];

            b.Append('|').Append(t.PadRight(inner)).AppendLine("|");
        }

        string Short(string value, int max) =>
            value.Length <= max ? value : value[..Math.Max(1, max - 1)] + "…";

        Border();
        Row($"VIMON  {spin} LIVE");
        Row("Q quit  P pause  <- / -> GPU");
        Border();

        // Status first: in short terminals the frame is truncated from the bottom.
        Row("STATUS");
        foreach (var diagnostic in diagnostics.Take(MaxStatusRows))
            Row(Short($"{diagnostic.Level}: {diagnostic.Message}", w - 4));

        Border();

        Row("CPU");
        Row(Short(s.Cpu?.Name ?? "Unknown CPU", w - 4));
        Row($"Temp        {FmtMetric(s.Cpu?.TempC, " C", positiveOnly: true)}");
        Row($"Load        {FmtMetric(s.Cpu?.LoadPct, " %")}");
        Row($"Power       {FmtMetric(s.Cpu?.PackagePowerW, " W", positiveOnly: true)}");

        if (s.Thermal is ThermalState thermal)
            Row($"Thermal     {thermal}");

        Border();

        Row($"GPU {(s.Gpus.Count == 0 ? "0/0" : $"{selectedGpu + 1}/{s.Gpus.Count}")}");

        if (gpu is null)
        {
            Row("No GPU detected");
        }
        else
        {
            Row(Short(gpu.Name, w - 4));
            Row($"Core        {FmtMetric(gpu.CoreTempC, " C", positiveOnly: true)}");
            Row($"Hotspot     {FmtMetric(gpu.HotspotTempC, " C", positiveOnly: true)}");
            Row($"VRAM temp   {FmtMetric(gpu.MemoryTempC, " C", positiveOnly: true)}");
            Row($"Load        {FmtMetric(gpu.UtilizationPct, " %")}");
            Row($"Memory      {GpuMemory(gpu)}");
            Row($"Power       {FmtMetric(gpu.PowerW, " W", positiveOnly: true)}");
            Row($"Fan         {FmtMetric(gpu.FanPct, " %")}");
            Row($"PCIe        {Pcie(gpu)}");
        }

        Border();

        Row("MEMORY");
        Row($"RAM         {s.Memory.UsedGb:0.0} / {s.Memory.TotalGb:0.0} GB");
        Row($"Usage       {s.Memory.LoadPct:0} %");

        if (s.Memory.Pressure is MemoryPressure pressure)
            Row($"Pressure    {pressure}");

        Border();

        Row("STORAGE");
        if (s.Storage.Count == 0)
        {
            Row("Temperature —");
        }
        else
        {
            foreach (var drive in s.Storage.Take(MaxStorageRows))
                Row($"{Short(drive.Name, Math.Max(12, w - 20))}  {FmtMetric(drive.TempC, " C", positiveOnly: true)}");
        }

        Border();

        return b.ToString();
    }

    private static string FmtMetric(double? value, string unit, bool positiveOnly = false)
    {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return "—";

        if (positiveOnly && value.Value <= 0)
            return "—";

        return $"{value.Value:0.#}{unit}";
    }

    private static string GpuMemory(GpuMetrics gpu)
    {
        if (gpu.MemoryUsedGb is not double used || double.IsNaN(used) || used < 0)
            return "—";

        return gpu.MemoryTotalGb is double total && total > 0
            ? $"{used:0.0} / {total:0.0} GB"
            : $"{used:0.0} GB";
    }

    private static string Pcie(GpuMetrics gpu)
    {
        if (!gpu.PcieGenerationCurrent.HasValue || !gpu.PcieWidthCurrent.HasValue)
            return "—";

        string current = $"Gen{gpu.PcieGenerationCurrent} x{gpu.PcieWidthCurrent}";

        if (gpu.PcieGenerationMax.HasValue && gpu.PcieWidthMax.HasValue)
            return $"{current} / max Gen{gpu.PcieGenerationMax} x{gpu.PcieWidthMax}";

        return current;
    }
}
