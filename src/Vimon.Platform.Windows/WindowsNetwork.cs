using System.Diagnostics;
using System.Net.NetworkInformation;
using Vimon.Core;

namespace Vimon.Platform.Windows;

/// <summary>
/// Network throughput per physical adapter. .NET reads the 64-bit counters of GetIfEntry2 here,
/// so no hand-written MIB_IF_ROW2 marshalling is needed.
/// </summary>
internal sealed class WindowsNetwork
{
    private readonly long _start = Stopwatch.GetTimestamp();
    private ByteRateTracker _tracker = ByteRateTracker.Empty;

    /// <returns>Rates of the physical adapters that are up; empty when the adapter list cannot be read.</returns>
    public IReadOnlyList<InterfaceRate> Read()
    {
        NetworkInterface[] adapters;

        try
        {
            adapters = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            return [];
        }

        var counters = adapters
            .Where(IsRelevant)
            .Select(ToCounters)
            .OfType<ByteCounters>()
            .Where(c => c.InBytes > 0 || c.OutBytes > 0)
            .ToList();

        var (rates, next) = _tracker.Advance(counters, Stopwatch.GetElapsedTime(_start));
        _tracker = next;
        return rates;
    }

    private static bool IsRelevant(NetworkInterface adapter) =>
        adapter.OperationalStatus == OperationalStatus.Up
        // Ppp covers L2TP, SSTP and dial-up VPNs, whose traffic also passes a physical adapter.
        && adapter.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp)
        && !NetworkInterfaceFilter.IsWindowsVirtualDescription(adapter.Description);

    /// <returns>Null for an adapter that went away after the list was read, so the others still count.</returns>
    private static ByteCounters? ToCounters(NetworkInterface adapter)
    {
        try
        {
            var statistics = adapter.GetIPStatistics();

            return new ByteCounters(
                adapter.Name,
                (ulong)Math.Max(0, statistics.BytesReceived),
                (ulong)Math.Max(0, statistics.BytesSent));
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException)
        {
            return null;
        }
    }
}
