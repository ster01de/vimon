using System.Buffers.Binary;
using System.Text;

namespace Vimon.Platform.MacOS;

/// <summary>One interface from the routing table, before filtering.</summary>
internal sealed record MacInterfaceCounters(string Name, int Flags, ulong InBytes, ulong OutBytes);

/// <summary>
/// Parses the buffer of <c>sysctl {CTL_NET, PF_ROUTE, 0, 0, NET_RT_IFLIST2, 0}</c>: one 160-byte
/// <c>if_msghdr2</c> per interface with 64-bit counters (<c>if_data64</c>), followed by its
/// <c>sockaddr_dl</c>, mixed with address messages that are skipped. Offsets were checked with
/// offsetof on macOS 13; the struct has no pointers, so arm64 and x64 share the layout. Pure.
/// </summary>
internal static class MacRouteMessages
{
    public const byte RtmIfInfo2 = 0x12;
    public const int HeaderLength = 160;

    private const int TypeOffset = 3;
    private const int AddrsOffset = 4;
    private const int FlagsOffset = 8;
    private const int InBytesOffset = 96;
    private const int OutBytesOffset = 104;
    private const int MinMessageLength = TypeOffset + 1;

    // The link-layer address that follows the header carries the interface name.
    private const int RtaIfp = 0x10;
    private const byte AfLink = 18;
    private const int SdlFamilyOffset = 1;
    private const int SdlNameLengthOffset = 5;
    private const int SdlNameOffset = 8;

    public static IReadOnlyList<MacInterfaceCounters> Parse(ReadOnlySpan<byte> buffer)
    {
        var interfaces = new List<MacInterfaceCounters>();
        int offset = 0;

        while (buffer.Length - offset >= MinMessageLength)
        {
            int length = BinaryPrimitives.ReadUInt16LittleEndian(buffer[offset..]);

            // A zero or overlong length means a corrupt or truncated buffer: stop instead of guessing.
            if (length < MinMessageLength || length > buffer.Length - offset)
                break;

            var message = buffer.Slice(offset, length);

            if (message[TypeOffset] == RtmIfInfo2 && length >= HeaderLength)
                interfaces.Add(ReadInterface(message));

            offset += length;
        }

        return interfaces;
    }

    private static MacInterfaceCounters ReadInterface(ReadOnlySpan<byte> message) => new(
        ReadName(message),
        BinaryPrimitives.ReadInt32LittleEndian(message[FlagsOffset..]),
        BinaryPrimitives.ReadUInt64LittleEndian(message[InBytesOffset..]),
        BinaryPrimitives.ReadUInt64LittleEndian(message[OutBytesOffset..]));

    /// <returns>The interface name, or an empty string when the message carries none.</returns>
    private static string ReadName(ReadOnlySpan<byte> message)
    {
        int addrs = BinaryPrimitives.ReadInt32LittleEndian(message[AddrsOffset..]);
        var link = message[HeaderLength..];

        if ((addrs & RtaIfp) == 0 || link.Length < SdlNameOffset || link[SdlFamilyOffset] != AfLink)
            return "";

        int nameLength = link[SdlNameLengthOffset];

        return nameLength <= link.Length - SdlNameOffset
            ? Encoding.ASCII.GetString(link.Slice(SdlNameOffset, nameLength))
            : "";
    }
}
