using Vimon.Core;

namespace Vimon.Platform.MacOS;

/// <summary>
/// macOS backend without root: Mach host statistics, sysctl, the IORegistry, NSProcessInfo and
/// read-only SMC access for temperatures, routing-table counters for network throughput. Power is not reported yet.
/// </summary>
public sealed class MacTelemetry : IPlatformTelemetry
{
    // mach_host_self() hands out one port right per call, so it is taken once and kept.
    private readonly uint _host;
    private readonly string _cpuName;
    private readonly MacMemory _memory;
    private readonly MacThermalState _thermal;
    private readonly MacTemperatures? _temperatures;
    private readonly MacNetwork _network = new();
    private readonly MacProcesses _processes = new();
    private readonly VolumeReader _volumes = new();
    private readonly MacDisks _disks = new();
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
        // hw.optional.arm64 is 1 on Apple Silicon, also for x64 processes running under Rosetta.
        _temperatures = MacTemperatures.TryCreate(isAppleSilicon: MacSysctl.Read<int>("hw.optional.arm64") == 1);
    }

    public string PlatformName => "macOS";

    public SystemSnapshot Read()
    {
        var temperatures = _temperatures?.Read() ?? MacTemperatureReading.None;
        var processes = _processes.Read();

        return new SystemSnapshot(
            DateTimeOffset.Now,
            ReadCpu(temperatures.CpuC),
            WithGpuTemperatures(MacGpuReader.Read(), temperatures),
            _memory.Read(),
            _disks.Read())
        {
            Thermal = _thermal.Read(),
            Network = _network.Read(),
            TopProcesses = processes.Top,
            TopDiskProcesses = processes.TopDisk,
            ProcessScope = ProcessScope.CurrentUser,
            Volumes = _volumes.Read()
        };
    }

    /// <summary>
    /// SMC GPU sensors belong to the integrated Apple GPU, or to the discrete GPU on Intel Macs,
    /// never to an Intel iGPU that sits next to a discrete one.
    /// </summary>
    internal static IReadOnlyList<GpuMetrics> WithGpuTemperatures(
        IReadOnlyList<GpuMetrics> gpus,
        MacTemperatureReading temperatures)
    {
        if (temperatures.GpuC is null)
            return gpus;

        int target = gpus.Count == 1
            ? 0
            : gpus.ToList().FindIndex(gpu => gpu.Vendor != Vendor.Intel);

        if (target < 0)
            return gpus;

        return gpus
            .Select((gpu, index) => index == target
                ? gpu with { CoreTempC = temperatures.GpuC, HotspotTempC = temperatures.GpuMaxC }
                : gpu)
            .ToList();
    }

    /// <remarks>
    /// Load needs two samples, so the very first read reports it as missing. Tick counters can lag
    /// behind short intervals (notably in VMs); then the baseline is kept and the last load repeated.
    /// </remarks>
    private CpuMetrics ReadCpu(double? tempC)
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

        return new CpuMetrics(_cpuName, TempC: tempC, LoadPct: _lastCpuLoadPct, PackagePowerW: null, ClockMhz: null);
    }

    public void Dispose() => _temperatures?.Dispose();
}
