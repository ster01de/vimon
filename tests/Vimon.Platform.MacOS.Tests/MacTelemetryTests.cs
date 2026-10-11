using Vimon.Core;

namespace Vimon.Platform.MacOS.Tests;

/// <summary>Integration tests against the real macOS APIs of the machine running them.</summary>
public class MacTelemetryTests
{
    private const int SampleIntervalMs = 100;
    private const int CpuLoadTimeoutMs = 2000;

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
    public void ReadsCpuNameAndLoadOnceTicksElapse()
    {
        using var telemetry = new MacTelemetry();

        CpuMetrics? first = telemetry.Read().Cpu;
        CpuMetrics? cpu = first;
        long deadline = Environment.TickCount64 + CpuLoadTimeoutMs;

        // Tick counters can lag behind short intervals in VMs, so poll instead of a fixed sleep.
        while (cpu?.LoadPct is null && Environment.TickCount64 < deadline)
        {
            Thread.Sleep(SampleIntervalMs);
            cpu = telemetry.Read().Cpu;
        }

        Assert.NotNull(first);
        Assert.Null(first.LoadPct);
        Assert.NotNull(cpu);
        Assert.False(string.IsNullOrWhiteSpace(cpu.Name));
        Assert.NotNull(cpu.LoadPct);
        Assert.InRange(cpu.LoadPct.Value, 0, 100);
    }

    [MacOSFact]
    public void ReportsPlausibleTemperaturesWhenTheSmcHasThem()
    {
        using var telemetry = new MacTelemetry();

        var snapshot = telemetry.Read();

        // VMs may expose no SMC temperature keys, so only reported values are checked.
        if (snapshot.Cpu?.TempC is double cpuC)
            Assert.InRange(cpuC, 1, 130);

        foreach (var gpu in snapshot.Gpus)
        {
            if (gpu.CoreTempC is double coreC)
                Assert.InRange(coreC, 1, gpu.HotspotTempC ?? 130);
        }
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
