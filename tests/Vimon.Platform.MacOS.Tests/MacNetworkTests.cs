using System.Buffers.Binary;
using System.Text;

namespace Vimon.Platform.MacOS.Tests;

public class MacNetworkTests
{
    private const int UpRunning = 0x41;
    private const int Loopback = 0x8;
    private const int SampleIntervalMs = 200;

    /// <summary>An if_msghdr2 with its sockaddr_dl, laid out like the kernel writes it.</summary>
    private static byte[] InterfaceMessage(string name, int flags, ulong inBytes, ulong outBytes, int addrs = 0x10)
    {
        var message = new byte[MacRouteMessages.HeaderLength + 20];
        BinaryPrimitives.WriteUInt16LittleEndian(message, (ushort)message.Length);
        message[2] = 5; // RTM_VERSION
        message[3] = MacRouteMessages.RtmIfInfo2;
        BinaryPrimitives.WriteInt32LittleEndian(message.AsSpan(4), addrs);
        BinaryPrimitives.WriteInt32LittleEndian(message.AsSpan(8), flags);
        BinaryPrimitives.WriteUInt64LittleEndian(message.AsSpan(96), inBytes);
        BinaryPrimitives.WriteUInt64LittleEndian(message.AsSpan(104), outBytes);

        var link = message.AsSpan(MacRouteMessages.HeaderLength);
        link[1] = 18; // AF_LINK
        link[5] = (byte)name.Length;
        Encoding.ASCII.GetBytes(name).CopyTo(link[8..]);
        return message;
    }

    private static byte[] OtherMessage(int length = 40)
    {
        var message = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(message, (ushort)length);
        message[3] = 0x13; // RTM_NEWMADDR2
        return message;
    }

    private static IReadOnlyList<MacInterfaceCounters> ReadRealTable()
    {
        byte[]? table = MacNetwork.ReadRouteTable();
        Assert.NotNull(table);
        return MacRouteMessages.Parse(table);
    }

    [Fact]
    public void ReadsNameFlagsAndCountersAtTheirOffsets()
    {
        var parsed = MacRouteMessages.Parse(InterfaceMessage("en0", 0x8863, 3_666_019_328, 5_000_000_000));

        Assert.Equal(new MacInterfaceCounters("en0", 0x8863, 3_666_019_328, 5_000_000_000), Assert.Single(parsed));
    }

    [Fact]
    public void SkipsMessagesOtherThanInterfaceInfo()
    {
        byte[] buffer = [.. OtherMessage(), .. InterfaceMessage("en0", UpRunning, 1, 2), .. OtherMessage(), .. InterfaceMessage("en7", UpRunning, 3, 4)];

        var names = MacRouteMessages.Parse(buffer).Select(i => i.Name);

        Assert.Equal(new[] { "en0", "en7" }, names);
    }

    [Fact]
    public void StopsAtAZeroOrTruncatedLengthWithoutThrowing()
    {
        var truncated = InterfaceMessage("en7", UpRunning, 3, 4)[..100];
        byte[] buffer = [.. InterfaceMessage("en0", UpRunning, 1, 2), .. truncated];

        Assert.Equal("en0", Assert.Single(MacRouteMessages.Parse(buffer)).Name);
        Assert.Empty(MacRouteMessages.Parse(new byte[64]));
    }

    [Fact]
    public void LeavesTheNameEmptyWithoutLinkAddress()
    {
        var parsed = MacRouteMessages.Parse(InterfaceMessage("en0", UpRunning, 1, 2, addrs: 0));

        Assert.Equal("", Assert.Single(parsed).Name);
    }

    [Theory]
    [InlineData("en0", UpRunning, 10UL, true)]
    [InlineData("en0", 0x1, 10UL, false)]
    [InlineData("lo0", UpRunning | Loopback, 10UL, false)]
    [InlineData("utun3", UpRunning, 10UL, false)]
    [InlineData("en3", UpRunning, 0UL, false)]
    public void KeepsOnlyRunningPhysicalInterfacesWithTraffic(string name, int flags, ulong bytes, bool expected) =>
        Assert.Equal(expected, MacNetwork.IsRelevant(new MacInterfaceCounters(name, flags, bytes, bytes)));

    [MacOSFact]
    public void ReadsTheRealRoutingTable()
    {
        var loopback = Assert.Single(ReadRealTable(), i => i.Name == "lo0");

        Assert.NotEqual(0, loopback.Flags & Loopback);
    }

    [MacOSFact]
    public void RealCountersNeverGoBackwards()
    {
        var first = ReadRealTable().ToDictionary(i => i.Name);
        Thread.Sleep(SampleIntervalMs);
        var second = ReadRealTable();

        foreach (var now in second.Where(i => first.ContainsKey(i.Name)))
        {
            Assert.True(now.InBytes >= first[now.Name].InBytes, $"{now.Name} in");
            Assert.True(now.OutBytes >= first[now.Name].OutBytes, $"{now.Name} out");
        }
    }

    [MacOSFact]
    public void TelemetryReportsNonNegativeRatesFromTheSecondSample()
    {
        using var telemetry = new MacTelemetry();
        telemetry.Read();
        Thread.Sleep(SampleIntervalMs);

        var network = telemetry.Read().Network;

        Assert.All(network, rate =>
        {
            Assert.True(rate.DownBytesPerSec is null or >= 0);
            Assert.True(rate.UpBytesPerSec is null or >= 0);
        });
    }
}
