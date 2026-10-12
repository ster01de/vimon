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

public sealed record StorageMetrics(string Name, double? TempC)
{
    /// <summary>Bytes per second; null until two samples exist or where the platform reports none.</summary>
    public double? ReadBytesPerSec { get; init; }

    public double? WriteBytesPerSec { get; init; }

    /// <summary>How the drive is attached, as the OS names it: USB, PCI-Express, Apple Fabric, Thunderbolt…</summary>
    public string? Interconnect { get; init; }

    public bool? IsExternal { get; init; }
}

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

    /// <summary>Physical network interfaces; empty where the backend does not read them yet.</summary>
    public IReadOnlyList<InterfaceRate> Network { get; init; } = [];

    /// <summary>The busiest processes the backend can see, busiest first; see <see cref="ProcessScope"/>.</summary>
    public IReadOnlyList<ProcessUsage> TopProcesses { get; init; } = [];

    public ProcessScope ProcessScope { get; init; } = ProcessScope.None;

    /// <summary>Free space of the user's volumes; empty where the backend does not read them.</summary>
    public IReadOnlyList<VolumeSpace> Volumes { get; init; } = [];
}

/// <summary>Bytes per second of one interface; null until two samples exist or after a counter reset.</summary>
public sealed record InterfaceRate(string Name, double? DownBytesPerSec, double? UpBytesPerSec);

public enum ThermalState { Nominal, Fair, Serious, Critical }

public interface IPlatformTelemetry : IDisposable
{
    string PlatformName { get; }
    SystemSnapshot Read();
}
