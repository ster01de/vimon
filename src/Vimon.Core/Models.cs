namespace Vimon.Core;

public enum Vendor { Unknown, Nvidia, Amd, Intel, Apple }

public sealed record GpuMetrics(
    int Index,
    string Name,
    Vendor Vendor,
    double? CoreTempC,
    double? HotspotTempC,
    double? MemoryTempC,
    double? UtilizationPct,
    double? PowerW,
    double? FanPct,
    double? CoreClockMhz,
    double? MemoryClockMhz,
    double? MemoryUsedGb,
    double? MemoryTotalGb,
    int? PcieGenerationCurrent,
    int? PcieWidthCurrent,
    int? PcieGenerationMax,
    int? PcieWidthMax
);

public sealed record CpuMetrics(
    string Name,
    double? TempC,
    double? LoadPct,
    double? PackagePowerW,
    double? ClockMhz
);

public sealed record MemoryMetrics(
    double UsedGb,
    double TotalGb,
    double LoadPct,
    double CommitUsedGb,
    double CommitTotalGb
)
{
    /// <summary>OS-reported memory pressure where the platform exposes it (macOS); otherwise null.</summary>
    public MemoryPressure? Pressure { get; init; }
}

public enum MemoryPressure { Normal, Warning, Critical }

public sealed record StorageMetrics(string Name, double? TempC);

public sealed record SystemSnapshot(
    DateTimeOffset Timestamp,
    CpuMetrics? Cpu,
    IReadOnlyList<GpuMetrics> Gpus,
    MemoryMetrics Memory,
    IReadOnlyList<StorageMetrics> Storage
)
{
    /// <summary>OS-reported thermal state where the platform exposes it (macOS); otherwise null.</summary>
    public ThermalState? Thermal { get; init; }
}

public enum ThermalState { Nominal, Fair, Serious, Critical }

public interface IPlatformTelemetry : IDisposable
{
    string PlatformName { get; }
    SystemSnapshot Read();
}
