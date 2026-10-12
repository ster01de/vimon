namespace Vimon.Core;

public enum HealthLevel { Good, Info, Warning, Critical }

public sealed record Diagnostic(HealthLevel Level, string Code, string Message);

public static class DiagnosticsEngine
{
    private const double PcieWidthCheckMinLoadPct = 50;
    // Binary gigabytes, like the RAM row and df -h.
    private const double BytesPerGb = 1024d * 1024 * 1024;

    public static IReadOnlyList<Diagnostic> Evaluate(SystemSnapshot s, HealthThresholds? thresholds = null)
    {
        var t = thresholds ?? HealthThresholds.Default;
        var list = new List<Diagnostic>();

        if (s.Cpu?.TempC is double cpu)
            AddByLevel(list, t.CpuTempC.Classify(cpu), "CPU_HOT", "CPU_WARM", $"CPU {cpu:0.#}°C");

        foreach (var g in s.Gpus)
        {
            if (g.CoreTempC is double core)
                AddByLevel(list, t.GpuCoreTempC.Classify(core), "GPU_HOT", "GPU_WARM", $"{g.Name}: GPU {core:0.#}°C");

            if (g.HotspotTempC is double hs)
                AddByLevel(list, t.GpuHotspotTempC.Classify(hs), "HOTSPOT_HOT", "HOTSPOT_WARM", $"{g.Name}: hotspot {hs:0.#}°C");

            // Conservative generic thresholds. Vendor/model profiles can override later.
            if (g.MemoryTempC is double mem)
                AddByLevel(list, t.GpuMemoryTempC.Classify(mem), "VRAM_HOT", "VRAM_WARM", $"{g.Name}: memory {mem:0.#}°C");

            if (g.PcieWidthCurrent.HasValue && g.PcieWidthMax.HasValue &&
                g.UtilizationPct.GetValueOrDefault() > PcieWidthCheckMinLoadPct &&
                g.PcieWidthCurrent.Value < g.PcieWidthMax.Value)
            {
                list.Add(new(HealthLevel.Info, "PCIE_WIDTH",
                    $"{g.Name}: PCIe x{g.PcieWidthCurrent} active, x{g.PcieWidthMax} supported"));
            }
        }

        AddByLevel(list, t.RamLoadPct.Classify(s.Memory.LoadPct), "RAM_PRESSURE", "RAM_PRESSURE",
            $"RAM {s.Memory.LoadPct:0}% used");

        if (s.Memory.Pressure == MemoryPressure.Critical)
            list.Add(new(HealthLevel.Critical, "MEMORY_PRESSURE", "OS memory pressure critical"));
        else if (s.Memory.Pressure == MemoryPressure.Warning)
            list.Add(new(HealthLevel.Warning, "MEMORY_PRESSURE", "OS memory pressure elevated"));

        if (s.Thermal == ThermalState.Critical)
            list.Add(new(HealthLevel.Critical, "THERMAL_CRITICAL", "System thermal state critical"));
        else if (s.Thermal == ThermalState.Serious)
            list.Add(new(HealthLevel.Warning, "THERMAL_SERIOUS", "System thermal state serious, throttling likely"));

        // Info, not a warning: the drive may only speak USB 2.0, but often a port, cable or hub is the limit.
        foreach (var drive in s.Storage.Where(d => d.UsbLink is UsbSpeed link && UsbSpeeds.IsSlow(link)))
            list.Add(new(HealthLevel.Info, "USB_LINK",
                $"{drive.Name}: {UsbSpeeds.Describe(drive.UsbLink!.Value)}, a USB 3 port, cable or hub can be up to 10x faster"));

        foreach (var volume in s.Volumes)
            AddByLevel(list, VolumeLevel(volume, t), "DISK_FULL", "DISK_LOW",
                $"{volume.Name}: {volume.FreeBytes / BytesPerGb:0.0} GB free ({volume.UsedPct:0}% used)");

        if (list.Count == 0)
            list.Add(new(HealthLevel.Good, "HEALTHY", "No important issues detected"));

        return list;
    }

    /// <summary>The fill level of a volume, unless it still has plenty of space in absolute terms.</summary>
    public static HealthLevel VolumeLevel(VolumeSpace volume, HealthThresholds thresholds) =>
        volume.FreeBytes / BytesPerGb >= HealthThresholds.VolumeFreeEnoughGb
            ? HealthLevel.Good
            : thresholds.VolumeUsedPct.Classify(volume.UsedPct);

    private static void AddByLevel(
        List<Diagnostic> list, HealthLevel level, string criticalCode, string warningCode, string message)
    {
        if (level == HealthLevel.Critical)
            list.Add(new(HealthLevel.Critical, criticalCode, message));
        else if (level == HealthLevel.Warning)
            list.Add(new(HealthLevel.Warning, warningCode, message));
    }
}
