using Vimon.Core;

namespace Vimon.Core.Tests;

public class DiagnosticsEngineTests
{
    private static readonly MemoryMetrics IdleMemory = new(4, 32, 12.5, 6, 40);

    private static SystemSnapshot Snapshot(
        CpuMetrics? cpu = null,
        IReadOnlyList<GpuMetrics>? gpus = null,
        MemoryMetrics? memory = null) =>
        new(DateTimeOffset.UnixEpoch, cpu, gpus ?? [], memory ?? IdleMemory, []);

    private static CpuMetrics Cpu(double? tempC) => new("Test CPU", tempC, 10, 50, 4000);

    private static GpuMetrics Gpu(
        double? coreTempC = null,
        double? hotspotTempC = null,
        double? memoryTempC = null,
        double? utilizationPct = null,
        int? pcieWidthCurrent = null,
        int? pcieWidthMax = null) =>
        new(0, "Test GPU", Vendor.Nvidia,
            coreTempC, hotspotTempC, memoryTempC, utilizationPct,
            null, null, null, null, null, null,
            4, pcieWidthCurrent, 4, pcieWidthMax);

    private static MemoryMetrics Ram(double loadPct) => new(16, 32, loadPct, 20, 40);

    [Fact]
    public void ReportsHealthyWhenNoMetricsAreAvailable()
    {
        var result = DiagnosticsEngine.Evaluate(Snapshot());

        var diagnostic = Assert.Single(result);
        Assert.Equal(HealthLevel.Good, diagnostic.Level);
        Assert.Equal("HEALTHY", diagnostic.Code);
    }

    [Theory]
    [InlineData(79.9, null)]
    [InlineData(80.0, "CPU_WARM")]
    [InlineData(89.9, "CPU_WARM")]
    [InlineData(90.0, "CPU_HOT")]
    public void ClassifiesCpuTemperature(double tempC, string? expectedCode) =>
        AssertSingleCode(Snapshot(cpu: Cpu(tempC)), expectedCode);

    [Theory]
    [InlineData(79.9, null)]
    [InlineData(80.0, "GPU_WARM")]
    [InlineData(85.0, "GPU_HOT")]
    public void ClassifiesGpuCoreTemperature(double tempC, string? expectedCode) =>
        AssertSingleCode(Snapshot(gpus: [Gpu(coreTempC: tempC)]), expectedCode);

    [Theory]
    [InlineData(94.9, null)]
    [InlineData(95.0, "HOTSPOT_WARM")]
    [InlineData(105.0, "HOTSPOT_HOT")]
    public void ClassifiesGpuHotspotTemperature(double tempC, string? expectedCode) =>
        AssertSingleCode(Snapshot(gpus: [Gpu(hotspotTempC: tempC)]), expectedCode);

    [Theory]
    [InlineData(94.9, null)]
    [InlineData(95.0, "VRAM_WARM")]
    [InlineData(105.0, "VRAM_HOT")]
    public void ClassifiesGpuMemoryTemperature(double tempC, string? expectedCode) =>
        AssertSingleCode(Snapshot(gpus: [Gpu(memoryTempC: tempC)]), expectedCode);

    [Theory]
    [InlineData(84.9, null, HealthLevel.Good)]
    [InlineData(85.0, "RAM_PRESSURE", HealthLevel.Warning)]
    [InlineData(95.0, "RAM_PRESSURE", HealthLevel.Critical)]
    public void ClassifiesRamPressure(double loadPct, string? expectedCode, HealthLevel expectedLevel)
    {
        var result = DiagnosticsEngine.Evaluate(Snapshot(memory: Ram(loadPct)));

        var diagnostic = Assert.Single(result);
        Assert.Equal(expectedCode ?? "HEALTHY", diagnostic.Code);
        Assert.Equal(expectedLevel, diagnostic.Level);
    }

    [Theory]
    [InlineData(8, 16, 51.0, "PCIE_WIDTH")]
    [InlineData(8, 16, 50.0, null)]
    [InlineData(16, 16, 99.0, null)]
    [InlineData(null, 16, 99.0, null)]
    public void FlagsReducedPcieWidthOnlyUnderLoad(
        int? widthCurrent, int? widthMax, double utilizationPct, string? expectedCode) =>
        AssertSingleCode(
            Snapshot(gpus: [Gpu(utilizationPct: utilizationPct, pcieWidthCurrent: widthCurrent, pcieWidthMax: widthMax)]),
            expectedCode);

    [Theory]
    [InlineData(ThermalState.Nominal, "HEALTHY", HealthLevel.Good)]
    [InlineData(ThermalState.Fair, "HEALTHY", HealthLevel.Good)]
    [InlineData(ThermalState.Serious, "THERMAL_SERIOUS", HealthLevel.Warning)]
    [InlineData(ThermalState.Critical, "THERMAL_CRITICAL", HealthLevel.Critical)]
    public void ClassifiesOsThermalState(ThermalState state, string expectedCode, HealthLevel expectedLevel)
    {
        var diagnostic = Assert.Single(DiagnosticsEngine.Evaluate(Snapshot() with { Thermal = state }));

        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal(expectedLevel, diagnostic.Level);
    }

    [Theory]
    [InlineData(MemoryPressure.Normal, "HEALTHY", HealthLevel.Good)]
    [InlineData(MemoryPressure.Warning, "MEMORY_PRESSURE", HealthLevel.Warning)]
    [InlineData(MemoryPressure.Critical, "MEMORY_PRESSURE", HealthLevel.Critical)]
    public void ClassifiesOsMemoryPressure(MemoryPressure pressure, string expectedCode, HealthLevel expectedLevel)
    {
        var snapshot = Snapshot(memory: IdleMemory with { Pressure = pressure });

        var diagnostic = Assert.Single(DiagnosticsEngine.Evaluate(snapshot));

        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal(expectedLevel, diagnostic.Level);
    }

    [Fact]
    public void ReportsIssuesFromEveryGpu()
    {
        var snapshot = Snapshot(gpus: [Gpu(coreTempC: 90), Gpu(coreTempC: 82)]);

        var codes = DiagnosticsEngine.Evaluate(snapshot).Select(d => d.Code).ToArray();

        Assert.Equal(new[] { "GPU_HOT", "GPU_WARM" }, codes);
    }

    private static void AssertSingleCode(SystemSnapshot snapshot, string? expectedCode)
    {
        var diagnostic = Assert.Single(DiagnosticsEngine.Evaluate(snapshot));
        Assert.Equal(expectedCode ?? "HEALTHY", diagnostic.Code);
    }
}
