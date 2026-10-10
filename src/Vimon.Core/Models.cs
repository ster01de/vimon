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
);

public sealed record StorageMetrics(string Name, double? TempC);

public sealed record SystemSnapshot(
    DateTimeOffset Timestamp,
    CpuMetrics? Cpu,
    IReadOnlyList<GpuMetrics> Gpus,
    MemoryMetrics Memory,
    IReadOnlyList<StorageMetrics> Storage
);

public interface IPlatformTelemetry : IDisposable
{
    string PlatformName { get; }
    SystemSnapshot Read();
}
