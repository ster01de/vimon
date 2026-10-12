using System.Diagnostics;
using System.Globalization;
using Vimon.Core;
using static Vimon.Platform.MacOS.MacNative;

namespace Vimon.Platform.MacOS;

/// <summary>Name and attachment of one drive, found by walking up from its storage driver.</summary>
internal sealed record MacDiskTopology(string Name, string? Interconnect, bool? IsExternal, bool IsDiskImage);

/// <summary>
/// Read and write throughput per drive from the "Statistics" of every IOBlockStorageDriver,
/// readable without root. Name and attachment (USB, internal …) come from the device above the
/// driver and are cached, because they do not change while the drive is attached. Disk images are
/// left out: their bytes also pass the drive that holds the image.
/// </summary>
internal sealed class MacDisks
{
    private const string DriverClass = "IOBlockStorageDriver";
    private const int MaxParentDepth = 12;

    private readonly Dictionary<ulong, MacDiskTopology> _topologies = [];
    private readonly long _start = Stopwatch.GetTimestamp();
    private ByteRateTracker _tracker = ByteRateTracker.Empty;

    public IReadOnlyList<StorageMetrics> Read()
    {
        var counters = new List<ByteCounters>();
        var drives = new Dictionary<string, MacDiskTopology>();

        MacRegistry.ForEachService(DriverClass, driver =>
        {
            if (IORegistryEntryGetRegistryEntryID(driver, out ulong id) != KernSuccess)
                return;

            var topology = TopologyOf(id, driver);
            var bytes = MacRegistry.WithDictionary(driver, "Statistics", ReadBytes);

            if (topology.IsDiskImage || bytes is null)
                return;

            var (read, written) = bytes.Value;
            string key = id.ToString(CultureInfo.InvariantCulture);
            counters.Add(new ByteCounters(key, (ulong)Math.Max(0, read), (ulong)Math.Max(0, written)));
            drives[key] = topology;
        });

        var (rates, next) = _tracker.Advance(counters, Stopwatch.GetElapsedTime(_start));
        _tracker = next;

        return rates
            .Select(rate => ToMetrics(drives[rate.Name], rate))
            .ToList();
    }

    internal static StorageMetrics ToMetrics(MacDiskTopology drive, InterfaceRate rate) =>
        new(drive.Name, TempC: null)
        {
            ReadBytesPerSec = rate.DownBytesPerSec,
            WriteBytesPerSec = rate.UpBytesPerSec,
            Interconnect = drive.Interconnect,
            IsExternal = drive.IsExternal
        };

    private static (long Read, long Written)? ReadBytes(IntPtr statistics)
    {
        long? read = MacRegistry.DictionaryInt64(statistics, "Bytes (Read)");
        long? written = MacRegistry.DictionaryInt64(statistics, "Bytes (Write)");
        return read is null || written is null ? null : (read.Value, written.Value);
    }

    private MacDiskTopology TopologyOf(ulong id, uint driver)
    {
        if (!_topologies.TryGetValue(id, out var topology))
        {
            topology = ReadTopology(driver);
            _topologies[id] = topology;
        }

        return topology;
    }

    /// <summary>The first "Protocol Characteristics" and "Device Characteristics" above the driver.</summary>
    private static MacDiskTopology ReadTopology(uint driver)
    {
        (string? Interconnect, string? Location)? protocol = null;
        (string? Vendor, string? Product)? device = null;
        uint current = driver;
        bool ownsCurrent = false;

        for (int depth = 0; depth < MaxParentDepth && (protocol is null || device is null); depth++)
        {
            protocol ??= MacRegistry.WithDictionary(current, "Protocol Characteristics", d =>
                ((string?, string?)?)(MacRegistry.DictionaryString(d, "Physical Interconnect"),
                    MacRegistry.DictionaryString(d, "Physical Interconnect Location")));
            device ??= MacRegistry.WithDictionary(current, "Device Characteristics", d =>
                ((string?, string?)?)(MacRegistry.DictionaryString(d, "Vendor Name"),
                    MacRegistry.DictionaryString(d, "Product Name")));

            if (IORegistryEntryGetParentEntry(current, MacRegistry.ServicePlane, out uint parent) != KernSuccess)
                break;

            if (ownsCurrent)
                IOObjectRelease(current);

            current = parent;
            ownsCurrent = true;
        }

        if (ownsCurrent)
            IOObjectRelease(current);

        return MacDiskNames.Describe(device?.Vendor, device?.Product, protocol?.Interconnect, protocol?.Location);
    }
}

/// <summary>Turns IORegistry strings into a drive description. Pure.</summary>
internal static class MacDiskNames
{
    public const string UnknownDrive = "Drive";

    private const string ExternalLocation = "External";
    private const string InternalLocation = "Internal";
    private const string DiskImageLocation = "File";

    public static MacDiskTopology Describe(string? vendor, string? product, string? interconnect, string? location)
    {
        string v = vendor?.Trim() ?? "";
        string p = product?.Trim() ?? "";

        string name = (v.Length, p.Length) switch
        {
            (> 0, > 0) when !p.StartsWith(v, StringComparison.OrdinalIgnoreCase) => $"{v} {p}",
            (_, > 0) => p,
            (> 0, _) => v,
            _ => UnknownDrive
        };

        bool? isExternal = location switch
        {
            ExternalLocation => true,
            InternalLocation => false,
            _ => null
        };

        return new MacDiskTopology(name, interconnect, isExternal, IsDiskImage: location == DiskImageLocation);
    }
}
