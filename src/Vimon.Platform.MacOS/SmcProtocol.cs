using System.Buffers.Binary;

namespace Vimon.Platform.MacOS;

internal readonly record struct SmcKeyInfo(uint DataSize, uint DataType);

/// <summary>
/// Request/response layout of the AppleSMC user client (SMCKeyData_t, 80 bytes, host byte order).
/// Pure byte handling so it can be tested without hardware.
/// </summary>
internal static class SmcProtocol
{
    public const int StructSize = 80;
    public const byte CommandReadBytes = 5;
    public const byte CommandReadIndex = 8;
    public const byte CommandReadKeyInfo = 9;
    public const byte ResultSuccess = 0;

    private const int KeyOffset = 0;
    private const int KeyInfoSizeOffset = 28;
    private const int KeyInfoTypeOffset = 32;
    private const int ResultOffset = 40;
    private const int CommandOffset = 42;
    private const int Data32Offset = 44;
    private const int BytesOffset = 48;
    private const int MaxDataBytes = 32;

    public static readonly uint TypeFloat = FourCc("flt ");
    public static readonly uint TypeSp78 = FourCc("sp78");
    public static readonly uint TypeUInt32 = FourCc("ui32");

    public static uint FourCc(string code)
    {
        if (code.Length != 4)
            throw new ArgumentException("SMC codes have exactly four characters", nameof(code));

        return (uint)code[0] << 24 | (uint)code[1] << 16 | (uint)code[2] << 8 | code[3];
    }

    public static string FourCcToString(uint value) =>
        string.Create(4, value, (chars, v) =>
        {
            chars[0] = (char)(byte)(v >> 24);
            chars[1] = (char)(byte)(v >> 16);
            chars[2] = (char)(byte)(v >> 8);
            chars[3] = (char)(byte)v;
        });

    public static byte[] ReadKeyInfoRequest(uint key) => Request(key, CommandReadKeyInfo, data32: 0, dataSize: 0);

    public static byte[] ReadBytesRequest(uint key, uint dataSize) => Request(key, CommandReadBytes, data32: 0, dataSize);

    public static byte[] ReadIndexRequest(uint index) => Request(key: 0, CommandReadIndex, data32: index, dataSize: 0);

    public static byte ResultOf(byte[] response) => response[ResultOffset];

    public static uint KeyOf(byte[] response) =>
        BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(KeyOffset));

    public static SmcKeyInfo KeyInfoOf(byte[] response) => new(
        BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(KeyInfoSizeOffset)),
        BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(KeyInfoTypeOffset)));

    public static byte[] BytesOf(byte[] response, uint dataSize) =>
        response.AsSpan(BytesOffset, (int)Math.Min(dataSize, MaxDataBytes)).ToArray();

    /// <summary>Apple Silicon reports "flt " (little-endian float), Intel Macs "sp78" (big-endian 8.8 fixed point).</summary>
    public static double? DecodeTemperature(SmcKeyInfo info, byte[] bytes)
    {
        if (info.DataType == TypeFloat && info.DataSize == 4 && bytes.Length >= 4)
            return BinaryPrimitives.ReadSingleLittleEndian(bytes);

        if (info.DataType == TypeSp78 && info.DataSize == 2 && bytes.Length >= 2)
            return BinaryPrimitives.ReadInt16BigEndian(bytes) / 256.0;

        return null;
    }

    public static bool IsTemperatureType(SmcKeyInfo info) =>
        (info.DataType == TypeFloat && info.DataSize == 4) ||
        (info.DataType == TypeSp78 && info.DataSize == 2);

    public static uint? DecodeUInt32(SmcKeyInfo info, byte[] bytes) =>
        info.DataType == TypeUInt32 && bytes.Length >= 4
            ? BinaryPrimitives.ReadUInt32BigEndian(bytes)
            : null;

    private static byte[] Request(uint key, byte command, uint data32, uint dataSize)
    {
        var request = new byte[StructSize];
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(KeyOffset), key);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(KeyInfoSizeOffset), dataSize);
        request[CommandOffset] = command;
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(Data32Offset), data32);
        return request;
    }
}
