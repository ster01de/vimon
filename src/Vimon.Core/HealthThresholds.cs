namespace Vimon.Core;

/// <summary>Warning and critical limits for one metric. A value at or above a limit reaches that level.</summary>
public sealed record Threshold(double Warning, double Critical)
{
    public HealthLevel Classify(double value) =>
        value >= Critical ? HealthLevel.Critical
        : value >= Warning ? HealthLevel.Warning
        : HealthLevel.Good;
}

/// <summary>
/// The limits behind STATUS and behind the colours of the live view, kept in one place so both
/// always agree. Profiles will provide other instances later.
/// </summary>
public sealed record HealthThresholds(
    Threshold CpuTempC,
    Threshold GpuCoreTempC,
    Threshold GpuHotspotTempC,
    Threshold GpuMemoryTempC,
    Threshold RamLoadPct,
    Threshold VolumeUsedPct)
{
    /// <summary>A volume with at least this much free space is fine however full it is in percent (e.g. 8 TB at 95 %).</summary>
    public const double VolumeFreeEnoughGb = 100;

    public static readonly HealthThresholds Default = new(
        CpuTempC: new Threshold(80, 90),
        GpuCoreTempC: new Threshold(80, 85),
        GpuHotspotTempC: new Threshold(95, 105),
        GpuMemoryTempC: new Threshold(95, 105),
        RamLoadPct: new Threshold(85, 95),
        VolumeUsedPct: new Threshold(90, 97));
}
