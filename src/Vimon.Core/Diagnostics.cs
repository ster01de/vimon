namespace Vimon.Core;

public enum HealthLevel { Good, Info, Warning, Critical }

public sealed record Diagnostic(HealthLevel Level, string Code, string Message);

public static class DiagnosticsEngine
{
    public static IReadOnlyList<Diagnostic> Evaluate(SystemSnapshot s, string profile = "default")
    {
        var list = new List<Diagnostic>();

        if (s.Cpu?.TempC is double cpu)
        {
            if (cpu >= 90) list.Add(new(HealthLevel.Critical, "CPU_HOT", $"CPU {cpu:0.#}°C"));
            else if (cpu >= 80) list.Add(new(HealthLevel.Warning, "CPU_WARM", $"CPU {cpu:0.#}°C"));
        }

        foreach (var g in s.Gpus)
        {
            if (g.CoreTempC is double core)
            {
                if (core >= 85) list.Add(new(HealthLevel.Critical, "GPU_HOT", $"{g.Name}: GPU {core:0.#}°C"));
                else if (core >= 80) list.Add(new(HealthLevel.Warning, "GPU_WARM", $"{g.Name}: GPU {core:0.#}°C"));
            }

            if (g.HotspotTempC is double hs)
            {
                if (hs >= 105) list.Add(new(HealthLevel.Critical, "HOTSPOT_HOT", $"{g.Name}: hotspot {hs:0.#}°C"));
                else if (hs >= 95) list.Add(new(HealthLevel.Warning, "HOTSPOT_WARM", $"{g.Name}: hotspot {hs:0.#}°C"));
            }

            // Conservative generic thresholds. Vendor/model profiles can override later.
            if (g.MemoryTempC is double mem)
            {
                if (mem >= 105) list.Add(new(HealthLevel.Critical, "VRAM_HOT", $"{g.Name}: memory {mem:0.#}°C"));
                else if (mem >= 95) list.Add(new(HealthLevel.Warning, "VRAM_WARM", $"{g.Name}: memory {mem:0.#}°C"));
            }

            if (g.PcieWidthCurrent.HasValue && g.PcieWidthMax.HasValue &&
                g.UtilizationPct.GetValueOrDefault() > 50 &&
                g.PcieWidthCurrent.Value < g.PcieWidthMax.Value)
            {
                list.Add(new(HealthLevel.Info, "PCIE_WIDTH",
                    $"{g.Name}: PCIe x{g.PcieWidthCurrent} active, x{g.PcieWidthMax} supported"));
            }
        }

        if (s.Memory.LoadPct >= 95)
            list.Add(new(HealthLevel.Critical, "RAM_PRESSURE", $"RAM {s.Memory.LoadPct:0}% used"));
        else if (s.Memory.LoadPct >= 85)
            list.Add(new(HealthLevel.Warning, "RAM_PRESSURE", $"RAM {s.Memory.LoadPct:0}% used"));

        if (list.Count == 0)
            list.Add(new(HealthLevel.Good, "HEALTHY", "No important issues detected"));

        return list;
    }
}
