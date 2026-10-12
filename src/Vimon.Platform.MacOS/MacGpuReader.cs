using Vimon.Core;
using static Vimon.Platform.MacOS.MacNative;

namespace Vimon.Platform.MacOS;

/// <summary>
/// Enumerates GPUs through IOAccelerator services in the IORegistry. Utilization and in-use memory
/// come from the driver's "PerformanceStatistics" dictionary. Temperatures and power need private
/// APIs and are not reported yet.
/// </summary>
internal static class MacGpuReader
{
    private const uint MainPortDefault = 0;
    private const double BytesPerGb = 1024d * 1024d * 1024d;

    public static IReadOnlyList<GpuMetrics> Read()
    {
        IntPtr matching = IOServiceMatching("IOAccelerator");

        if (matching == IntPtr.Zero)
            return [];

        if (IOServiceGetMatchingServices(MainPortDefault, matching, out uint iterator) != KernSuccess)
            return [];

        var gpus = new List<GpuMetrics>();

        try
        {
            for (uint entry = IOIteratorNext(iterator); entry != 0; entry = IOIteratorNext(iterator))
            {
                try
                {
                    gpus.Add(ReadGpu(entry, gpus.Count));
                }
                finally
                {
                    IOObjectRelease(entry);
                }
            }
        }
        finally
        {
            IOObjectRelease(iterator);
        }

        return gpus;
    }

    internal static Vendor VendorOf(string name)
    {
        if (name.StartsWith("Apple", StringComparison.OrdinalIgnoreCase))
            return Vendor.Apple;

        if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
            return Vendor.Amd;

        if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            return Vendor.Intel;

        if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("GeForce", StringComparison.OrdinalIgnoreCase))
            return Vendor.Nvidia;

        return Vendor.Unknown;
    }

    private static GpuMetrics ReadGpu(uint entry, int index)
    {
        string name = ReadModel(entry) ?? $"GPU {index + 1}";
        double? utilizationPct = null;
        double? memoryUsedBytes = null;

        IntPtr stats = MacRegistry.CopyProperty(entry, "PerformanceStatistics");

        if (stats != IntPtr.Zero)
        {
            try
            {
                if (CFGetTypeID(stats) == CFDictionaryGetTypeID())
                {
                    utilizationPct = MacRegistry.DictionaryNumber(stats, "Device Utilization %");
                    memoryUsedBytes = MacRegistry.DictionaryNumber(stats, "In use system memory");
                }
            }
            finally
            {
                CFRelease(stats);
            }
        }

        return new GpuMetrics(
            index, name, VendorOf(name),
            CoreTempC: null, HotspotTempC: null, MemoryTempC: null,
            UtilizationPct: utilizationPct,
            PowerW: null, FanPct: null, CoreClockMhz: null, MemoryClockMhz: null,
            MemoryUsedGb: memoryUsedBytes / BytesPerGb,
            MemoryTotalGb: null,
            PcieGenerationCurrent: null, PcieWidthCurrent: null,
            PcieGenerationMax: null, PcieWidthMax: null);
    }

    /// <summary>Apple Silicon exposes "model" on the accelerator; discrete GPUs on its PCI parent.</summary>
    private static string? ReadModel(uint entry)
    {
        string? model = MacRegistry.ReadStringProperty(entry, "model");

        if (model is not null)
            return model;

        if (IORegistryEntryGetParentEntry(entry, "IOService", out uint parent) != KernSuccess)
            return null;

        try
        {
            return MacRegistry.ReadStringProperty(parent, "model");
        }
        finally
        {
            IOObjectRelease(parent);
        }
    }
}
