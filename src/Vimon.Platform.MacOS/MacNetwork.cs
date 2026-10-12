using System.Diagnostics;
using System.Runtime.InteropServices;
using Vimon.Core;

namespace Vimon.Platform.MacOS;

/// <summary>
/// Network throughput per physical interface from the 64-bit routing-table counters. No root
/// needed. getifaddrs is not used because its 32-bit counters wrap every 4 GB.
/// </summary>
internal sealed class MacNetwork
{
    private const int CtlNet = 4;
    private const int PfRoute = 17;
    private const int NetRtIfList2 = 6;
    private const int IffUp = 0x1;
    private const int IffLoopback = 0x8;
    private const int IffRunning = 0x40;
    private const int Enomem = 12;
    private const int MaxReadAttempts = 3;

    private static readonly int[] Mib = [CtlNet, PfRoute, 0, 0, NetRtIfList2, 0];

    private readonly long _start = Stopwatch.GetTimestamp();
    private ByteRateTracker _tracker = ByteRateTracker.Empty;

    /// <returns>Rates of the physical interfaces that are up; empty when the table cannot be read.</returns>
    public IReadOnlyList<InterfaceRate> Read()
    {
        byte[]? table = ReadRouteTable();

        if (table is null)
            return [];

        var counters = MacRouteMessages.Parse(table)
            .Where(IsRelevant)
            .Select(i => new ByteCounters(i.Name, i.InBytes, i.OutBytes))
            .ToList();

        var (rates, next) = _tracker.Advance(counters, Stopwatch.GetElapsedTime(_start));
        _tracker = next;
        return rates;
    }

    /// <summary>Up and running, not loopback, not virtual, and has carried traffic at some point.</summary>
    internal static bool IsRelevant(MacInterfaceCounters i) =>
        (i.Flags & (IffUp | IffRunning)) == (IffUp | IffRunning)
        && (i.Flags & IffLoopback) == 0
        && NetworkInterfaceFilter.IsMacPhysicalName(i.Name)
        && (i.InBytes > 0 || i.OutBytes > 0);

    internal static byte[]? ReadRouteTable()
    {
        for (int attempt = 0; attempt < MaxReadAttempts; attempt++)
        {
            nuint length = 0;

            if (MacNative.sysctl(Mib, (uint)Mib.Length, null, ref length, IntPtr.Zero, 0) != 0 || length == 0)
                return null;

            // Interfaces can appear between the two calls, so leave headroom.
            length += length / 4;
            var buffer = new byte[length];

            if (MacNative.sysctl(Mib, (uint)Mib.Length, buffer, ref length, IntPtr.Zero, 0) == 0)
                return buffer[..(int)length];

            // ENOMEM: the table grew past the headroom; ask for the size again.
            if (Marshal.GetLastPInvokeError() != Enomem)
                return null;
        }

        return null;
    }
}
