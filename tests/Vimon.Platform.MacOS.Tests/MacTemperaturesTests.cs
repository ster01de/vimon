using Vimon.Core;

namespace Vimon.Platform.MacOS.Tests;

public class MacTemperaturesTests
{
    [Theory]
    [InlineData("Tp0A", true, SmcSensorGroup.Cpu)]
    [InlineData("Te05", true, SmcSensorGroup.Cpu)]
    [InlineData("Tg0f", true, SmcSensorGroup.Gpu)]
    [InlineData("TC0P", true, null)]
    [InlineData("TG0D", true, null)]
    [InlineData("TC0P", false, SmcSensorGroup.Cpu)]
    [InlineData("TG0D", false, SmcSensorGroup.Gpu)]
    [InlineData("Tp0P", false, null)]
    [InlineData("TB0T", true, null)]
    public void ClassifiesSensorKeysPerArchitecture(string key, bool isAppleSilicon, SmcSensorGroup? expected) =>
        Assert.Equal(expected, MacTemperatures.Classify(key, isAppleSilicon));

    [Fact]
    public void AveragesCpuAndReportsGpuAverageAndMax()
    {
        var reading = MacTemperatures.Aggregate(
        [
            (SmcSensorGroup.Cpu, 40.0),
            (SmcSensorGroup.Cpu, 60.0),
            (SmcSensorGroup.Gpu, 36.0),
            (SmcSensorGroup.Gpu, 42.0)
        ]);

        Assert.Equal(new MacTemperatureReading(50.0, 39.0, 42.0), reading);
    }

    [Fact]
    public void DropsImplausibleAndMissingValues()
    {
        var reading = MacTemperatures.Aggregate(
        [
            (SmcSensorGroup.Cpu, 0.0),
            (SmcSensorGroup.Cpu, 55.0),
            (SmcSensorGroup.Cpu, 300.0),
            (SmcSensorGroup.Gpu, null)
        ]);

        Assert.Equal(new MacTemperatureReading(55.0, null, null), reading);
    }

    [Fact]
    public void AssignsGpuTemperaturesToTheOnlyGpu()
    {
        var gpus = new[] { Gpu("Apple M2", Vendor.Apple) };

        var result = MacTelemetry.WithGpuTemperatures(gpus, new MacTemperatureReading(50.0, 40.0, 45.0));

        Assert.Equal(40.0, result[0].CoreTempC);
        Assert.Equal(45.0, result[0].HotspotTempC);
    }

    [Fact]
    public void AssignsGpuTemperaturesToTheDiscreteGpuNextToAnIntelGpu()
    {
        var gpus = new[] { Gpu("Intel UHD Graphics 630", Vendor.Intel), Gpu("AMD Radeon Pro 5500M", Vendor.Amd) };

        var result = MacTelemetry.WithGpuTemperatures(gpus, new MacTemperatureReading(50.0, 60.0, 70.0));

        Assert.Null(result[0].CoreTempC);
        Assert.Equal(60.0, result[1].CoreTempC);
    }

    [Fact]
    public void LeavesGpusUnchangedWithoutGpuTemperature()
    {
        var gpus = new[] { Gpu("Apple M2", Vendor.Apple) };

        Assert.Same(gpus, MacTelemetry.WithGpuTemperatures(gpus, MacTemperatureReading.None));
    }

    private static GpuMetrics Gpu(string name, Vendor vendor) =>
        new(0, name, vendor,
            null, null, null, null, null, null, null, null, null, null,
            null, null, null, null);
}
