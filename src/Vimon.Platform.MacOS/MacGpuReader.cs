using System.Runtime.InteropServices;
using System.Text;
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
    private const int MaxNameBytes = 256;
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

        IntPtr stats = CopyProperty(entry, "PerformanceStatistics");

        if (stats != IntPtr.Zero)
        {
            try
            {
                if (CFGetTypeID(stats) == CFDictionaryGetTypeID())
                {
                    utilizationPct = DictionaryNumber(stats, "Device Utilization %");
                    memoryUsedBytes = DictionaryNumber(stats, "In use system memory");
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
        string? model = ReadStringProperty(entry, "model");

        if (model is not null)
            return model;

        if (IORegistryEntryGetParentEntry(entry, "IOService", out uint parent) != KernSuccess)
            return null;

        try
        {
            return ReadStringProperty(parent, "model");
        }
        finally
        {
            IOObjectRelease(parent);
        }
    }

    private static string? ReadStringProperty(uint entry, string key)
    {
        IntPtr value = CopyProperty(entry, key);

        if (value == IntPtr.Zero)
            return null;

        try
        {
            nuint type = CFGetTypeID(value);

            if (type == CFStringGetTypeID())
            {
                var buffer = new byte[MaxNameBytes];
                return CFStringGetCString(value, buffer, buffer.Length, CFStringEncodingUtf8)
                    ? DecodeNullTerminated(buffer)
                    : null;
            }

            if (type == CFDataGetTypeID())
            {
                int length = (int)Math.Min(CFDataGetLength(value), MaxNameBytes);

                if (length <= 0)
                    return null;

                var bytes = new byte[length];
                Marshal.Copy(CFDataGetBytePtr(value), bytes, 0, length);
                return DecodeNullTerminated(bytes);
            }

            return null;
        }
        finally
        {
            CFRelease(value);
        }
    }

    /// <returns>An owned CF object (caller releases), or <see cref="IntPtr.Zero"/>.</returns>
    private static IntPtr CopyProperty(uint entry, string key)
    {
        IntPtr cfKey = CFStringCreateWithCString(IntPtr.Zero, key, CFStringEncodingUtf8);

        if (cfKey == IntPtr.Zero)
            return IntPtr.Zero;

        try
        {
            return IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    private static double? DictionaryNumber(IntPtr dictionary, string key)
    {
        IntPtr cfKey = CFStringCreateWithCString(IntPtr.Zero, key, CFStringEncodingUtf8);

        if (cfKey == IntPtr.Zero)
            return null;

        try
        {
            // Borrowed reference: owned by the dictionary, must not be released.
            IntPtr value = CFDictionaryGetValue(dictionary, cfKey);

            if (value == IntPtr.Zero || CFGetTypeID(value) != CFNumberGetTypeID())
                return null;

            // A false result only signals a lossy conversion; the value is still the closest double.
            CFNumberGetValue(value, CFNumberDoubleType, out double number);
            return number;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    private static string? DecodeNullTerminated(byte[] bytes)
    {
        int end = Array.IndexOf(bytes, (byte)0);
        string text = Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end).Trim();
        return text.Length == 0 ? null : text;
    }
}
