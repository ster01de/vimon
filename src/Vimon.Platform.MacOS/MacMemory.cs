using Vimon.Core;

namespace Vimon.Platform.MacOS;

internal sealed class MacMemory
{
    private const double BytesPerGb = 1024d * 1024d * 1024d;

    // kern.memorystatus_vm_pressure_level uses the DISPATCH_MEMORYPRESSURE_* values.
    private const int PressureNormal = 1;
    private const int PressureWarning = 2;
    private const int PressureCritical = 4;

    private readonly uint _host;
    private readonly ulong _pageSize;
    private readonly ulong _totalBytes;

    public MacMemory(uint host)
    {
        _host = host;

        if (MacNative.host_page_size(host, out nuint pageSize) != MacNative.KernSuccess || pageSize == 0)
            throw new InvalidOperationException("host_page_size failed");

        _pageSize = pageSize;
        _totalBytes = MacSysctl.Read<ulong>("hw.memsize")
            ?? throw new InvalidOperationException("sysctl hw.memsize failed");
    }

    public MemoryMetrics Read()
    {
        uint count = MacNative.HostVmInfo64Count;

        if (MacNative.host_statistics64(_host, MacNative.HostVmInfo64, out var vm, ref count) != MacNative.KernSuccess)
            throw new InvalidOperationException("host_statistics64(HOST_VM_INFO64) failed");

        ulong usedBytes = UsedBytes(vm, _pageSize);
        var swap = MacSysctl.Read<SwapUsage>("vm.swapusage");

        double totalGb = _totalBytes / BytesPerGb;
        double usedGb = usedBytes / BytesPerGb;
        double swapUsedGb = (swap?.Used ?? 0) / BytesPerGb;
        double swapTotalGb = (swap?.Total ?? 0) / BytesPerGb;

        return new MemoryMetrics(
            usedGb,
            totalGb,
            totalGb > 0 ? usedGb / totalGb * 100.0 : 0,
            usedGb + swapUsedGb,
            totalGb + swapTotalGb)
        {
            Pressure = MapPressure(MacSysctl.Read<int>("kern.memorystatus_vm_pressure_level"))
        };
    }

    /// <summary>
    /// "Memory Used" as Activity Monitor shows it: app memory (anonymous minus purgeable)
    /// plus wired plus compressed. File cache is excluded because the OS reclaims it freely.
    /// </summary>
    public static ulong UsedBytes(VmStatistics64 vm, ulong pageSize)
    {
        ulong appPages = vm.InternalPageCount > vm.PurgeableCount
            ? vm.InternalPageCount - vm.PurgeableCount
            : 0;

        return (appPages + vm.WireCount + vm.CompressorPageCount) * pageSize;
    }

    public static MemoryPressure? MapPressure(int? level) => level switch
    {
        PressureNormal => MemoryPressure.Normal,
        PressureWarning => MemoryPressure.Warning,
        PressureCritical => MemoryPressure.Critical,
        _ => null
    };
}
