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
    private double? _lastCpuLoadPct;

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

    /// <remarks>
    /// Load needs two samples, so the very first read reports it as missing. Tick counters can lag
    /// behind short intervals (notably in VMs); then the baseline is kept and the last load repeated.
    /// </remarks>
    private CpuMetrics ReadCpu()
    {
        uint count = MacNative.HostCpuLoadInfoCount;

        if (MacNative.host_statistics(_host, MacNative.HostCpuLoadInfo, out var ticks, ref count) == MacNative.KernSuccess)
        {
            double? loadPct = _previousCpuTicks is CpuLoadInfo previous
                ? MacCpuLoad.Between(previous, ticks)
                : null;

            if (loadPct is not null || _previousCpuTicks is null)
                _previousCpuTicks = ticks;

            _lastCpuLoadPct = loadPct ?? _lastCpuLoadPct;
        }

        return new CpuMetrics(_cpuName, TempC: null, LoadPct: _lastCpuLoadPct, PackagePowerW: null, ClockMhz: null);
    }

    public void Dispose()
    {
    }
}
