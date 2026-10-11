using Vimon.Core;

namespace Vimon.Platform.MacOS;

/// <summary>
/// macOS backend built on public APIs only: Mach host statistics, sysctl, the IORegistry and
/// NSProcessInfo. CPU/GPU temperatures and power need private APIs and are reported as missing.
/// </summary>
public sealed class MacTelemetry : IPlatformTelemetry
{
    // mach_host_self() hands out one port right per call, so it is taken once and kept.
    private readonly uint _host;
    private readonly string _cpuName;
    private readonly MacMemory _memory;
    private readonly MacThermalState _thermal;
    private CpuLoadInfo? _previousCpuTicks;

    public MacTelemetry()
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("The macOS backend only runs on macOS.");

        _host = MacNative.mach_host_self();
        _cpuName = MacSysctl.ReadString("machdep.cpu.brand_string") ?? "Unknown CPU";
        _memory = new MacMemory(_host);
        _thermal = new MacThermalState();
    }

    public string PlatformName => "macOS";

    public SystemSnapshot Read() =>
        new(DateTimeOffset.Now, ReadCpu(), MacGpuReader.Read(), _memory.Read(), [])
        {
            Thermal = _thermal.Read()
        };

    /// <remarks>Load needs two samples, so the very first read reports it as missing.</remarks>
    private CpuMetrics ReadCpu()
    {
        uint count = MacNative.HostCpuLoadInfoCount;
        double? loadPct = null;

        if (MacNative.host_statistics(_host, MacNative.HostCpuLoadInfo, out var ticks, ref count) == MacNative.KernSuccess)
        {
            if (_previousCpuTicks is CpuLoadInfo previous)
                loadPct = MacCpuLoad.Between(previous, ticks);

            _previousCpuTicks = ticks;
        }

        return new CpuMetrics(_cpuName, TempC: null, LoadPct: loadPct, PackagePowerW: null, ClockMhz: null);
    }

    public void Dispose()
    {
    }
}
