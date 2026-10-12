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
        List<ByteCounters> counters;

        try
        {
            counters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsRelevant)
                .Select(ToCounters)
                .Where(c => c.InBytes > 0 || c.OutBytes > 0)
                .ToList();
        }
        catch (NetworkInformationException)
        {
            return [];
        }

        var (rates, next) = _tracker.Advance(counters, Stopwatch.GetElapsedTime(_start));
        _tracker = next;
        return rates;
    }

    private static bool IsRelevant(NetworkInterface adapter) =>
        adapter.OperationalStatus == OperationalStatus.Up
        && adapter.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
        && !NetworkInterfaceFilter.IsWindowsVirtualDescription(adapter.Description);

    private static ByteCounters ToCounters(NetworkInterface adapter)
    {
        var statistics = adapter.GetIPStatistics();

        return new ByteCounters(
            adapter.Name,
            (ulong)Math.Max(0, statistics.BytesReceived),
            (ulong)Math.Max(0, statistics.BytesSent));
    }
}
