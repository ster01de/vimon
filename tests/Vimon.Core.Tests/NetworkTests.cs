using Vimon.Core;

namespace Vimon.Core.Tests;

public class ByteRateTrackerTests
{
    private static readonly TimeSpan Start = TimeSpan.FromSeconds(10);

    private static ByteCounters En0(ulong inBytes, ulong outBytes) => new("en0", inBytes, outBytes);

    [Fact]
    public void ReportsNoRateOnTheFirstSample()
    {
        var (rates, _) = ByteRateTracker.Empty.Advance([En0(1000, 500)], Start);

        Assert.Equal(new InterfaceRate("en0", null, null), Assert.Single(rates));
    }

    [Fact]
    public void DividesTheCounterDeltaByTheElapsedTime()
    {
        var (_, tracker) = ByteRateTracker.Empty.Advance([En0(1000, 500)], Start);

        var (rates, _) = tracker.Advance([En0(3000, 1500)], Start + TimeSpan.FromSeconds(0.5));

        Assert.Equal(new InterfaceRate("en0", 4000, 2000), Assert.Single(rates));
    }

    [Fact]
    public void ReportsNoRateForADirectionWhoseCounterWentBackwards()
    {
        var (_, tracker) = ByteRateTracker.Empty.Advance([En0(1000, 500)], Start);

        var (rates, _) = tracker.Advance([En0(10, 1500)], Start + TimeSpan.FromSeconds(1));

        Assert.Equal(new InterfaceRate("en0", null, 1000), Assert.Single(rates));
    }

    [Fact]
    public void ReportsNoRateForAnInterfaceThatJustAppeared()
    {
        var (_, tracker) = ByteRateTracker.Empty.Advance([En0(1000, 500)], Start);

        var (rates, _) = tracker.Advance([En0(2000, 500), new ByteCounters("en7", 50, 50)], Start + TimeSpan.FromSeconds(1));

        Assert.Equal(new InterfaceRate("en0", 1000, 0), rates[0]);
        Assert.Equal(new InterfaceRate("en7", null, null), rates[1]);
    }

    [Fact]
    public void ForgetsInterfacesThatDisappeared()
    {
        var (_, first) = ByteRateTracker.Empty.Advance([En0(1000, 500)], Start);
        var (_, second) = first.Advance([], Start + TimeSpan.FromSeconds(1));

        var (rates, _) = second.Advance([En0(5000, 500)], Start + TimeSpan.FromSeconds(2));

        Assert.Equal(new InterfaceRate("en0", null, null), Assert.Single(rates));
    }

    [Fact]
    public void ReportsNoRateWhenNoTimePassed()
    {
        var (_, tracker) = ByteRateTracker.Empty.Advance([En0(1000, 500)], Start);

        var (rates, _) = tracker.Advance([En0(2000, 600)], Start);

        Assert.Equal(new InterfaceRate("en0", null, null), Assert.Single(rates));
    }

    [Fact]
    public void LeavesTheEarlierTrackerUnchanged()
    {
        var (_, tracker) = ByteRateTracker.Empty.Advance([En0(1000, 500)], Start);
        _ = tracker.Advance([En0(9000, 9000)], Start + TimeSpan.FromSeconds(1));

        var (rates, _) = tracker.Advance([En0(2000, 1500)], Start + TimeSpan.FromSeconds(1));

        Assert.Equal(new InterfaceRate("en0", 1000, 1000), Assert.Single(rates));
    }
}

public class NetworkTotalsTests
{
    [Fact]
    public void SumsTheKnownRatesPerDirection()
    {
        var totals = NetworkTotals.Of([new("en0", 1000, null), new("en7", 500, null)]);

        Assert.Equal(((double?)1500.0, (double?)null), totals);
    }

    [Fact]
    public void IsUnknownWithoutInterfaces()
    {
        Assert.Equal(((double?)null, (double?)null), NetworkTotals.Of([]));
    }
}

public class NetworkInterfaceFilterTests
{
    [Theory]
    [InlineData("en0", true)]
    [InlineData("en11", true)]
    [InlineData("utun3", false)]
    [InlineData("awdl0", false)]
    [InlineData("llw0", false)]
    [InlineData("ap1", false)]
    [InlineData("bridge0", false)]
    [InlineData("anpi0", false)]
    [InlineData("", false)]
    public void KeepsOnlyPhysicalMacInterfaces(string name, bool expected) =>
        Assert.Equal(expected, NetworkInterfaceFilter.IsMacPhysicalName(name));

    [Theory]
    [InlineData("Intel(R) Ethernet Connection I219-V", false)]
    [InlineData("Realtek PCIe 2.5GbE Family Controller", false)]
    [InlineData("Hyper-V Virtual Ethernet Adapter", true)]
    [InlineData("WireGuard Tunnel", true)]
    [InlineData("TAP-Windows Adapter V9", true)]
    [InlineData("Bluetooth Device (Personal Area Network)", true)]
    public void RecognisesVirtualWindowsAdapters(string description, bool expected) =>
        Assert.Equal(expected, NetworkInterfaceFilter.IsWindowsVirtualDescription(description));
}
