namespace Vimon.Platform.MacOS.Tests;

/// <summary>Integration tests against the real macOS APIs of the machine running them.</summary>
public class MacTelemetryTests
{
    private const int SampleIntervalMs = 250;

    [MacOSFact]
    public void ReadsPhysicalMemory()
    {
        using var telemetry = new MacTelemetry();

        var memory = telemetry.Read().Memory;

        Assert.InRange(memory.TotalGb, 1, 4096);
        Assert.InRange(memory.UsedGb, 0.01, memory.TotalGb);
        Assert.InRange(memory.LoadPct, 0, 100);
        Assert.True(memory.CommitTotalGb >= memory.TotalGb);
        Assert.NotNull(memory.Pressure);
    }

    [MacOSFact]
    public void ReadsCpuNameAndLoadFromSecondSample()
    {
        using var telemetry = new MacTelemetry();

        var first = telemetry.Read().Cpu;
        Thread.Sleep(SampleIntervalMs);
        var second = telemetry.Read().Cpu;

        Assert.NotNull(first);
        Assert.Null(first.LoadPct);
        Assert.NotNull(second);
        Assert.False(string.IsNullOrWhiteSpace(second.Name));
        Assert.NotNull(second.LoadPct);
        Assert.InRange(second.LoadPct.Value, 0, 100);
    }

    [MacOSFact]
    public void ReadsThermalState()
    {
        using var telemetry = new MacTelemetry();

        Assert.NotNull(telemetry.Read().Thermal);
    }

    [MacOSFact]
    public void ReportsGpusWithPlausibleValues()
    {
        using var telemetry = new MacTelemetry();

        // CI runners are VMs that may expose no accelerator, so only the values are checked.
        foreach (var gpu in telemetry.Read().Gpus)
        {
            Assert.False(string.IsNullOrWhiteSpace(gpu.Name));

            if (gpu.UtilizationPct is double utilization)
                Assert.InRange(utilization, 0, 100);

            if (gpu.MemoryUsedGb is double usedGb)
                Assert.True(usedGb >= 0);
        }
    }
}
