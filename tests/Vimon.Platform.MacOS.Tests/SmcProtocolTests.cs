using System.Buffers.Binary;

namespace Vimon.Platform.MacOS.Tests;

public class SmcProtocolTests
{
    [Fact]
    public void ConvertsFourCharacterCodesBothWays()
    {
        uint key = SmcProtocol.FourCc("Tp0A");

        Assert.Equal(0x5470_3041u, key);
        Assert.Equal("Tp0A", SmcProtocol.FourCcToString(key));
    }

    [Fact]
    public void RejectsCodesThatAreNotFourCharacters() =>
        Assert.Throws<ArgumentException>(() => SmcProtocol.FourCc("Tp0"));

    [Fact]
    public void BuildsReadBytesRequestAtTheDriverOffsets()
    {
        uint key = SmcProtocol.FourCc("Tg0f");

        byte[] request = SmcProtocol.ReadBytesRequest(key, dataSize: 4);

        Assert.Equal(SmcProtocol.StructSize, request.Length);
        Assert.Equal(key, BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(0)));
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(28)));
        Assert.Equal(SmcProtocol.CommandReadBytes, request[42]);
    }

    [Fact]
    public void BuildsReadIndexRequestWithIndexInData32()
    {
        byte[] request = SmcProtocol.ReadIndexRequest(1234);

        Assert.Equal(SmcProtocol.CommandReadIndex, request[42]);
        Assert.Equal(1234u, BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(44)));
    }

    [Fact]
    public void ParsesKeyInfoResultAndBytesFromResponse()
    {
        var response = new byte[SmcProtocol.StructSize];
        BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(28), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(32), SmcProtocol.TypeFloat);
        BinaryPrimitives.WriteSingleLittleEndian(response.AsSpan(48), 42.5f);

        var info = SmcProtocol.KeyInfoOf(response);

        Assert.Equal(new SmcKeyInfo(4, SmcProtocol.TypeFloat), info);
        Assert.Equal(SmcProtocol.ResultSuccess, SmcProtocol.ResultOf(response));
        Assert.Equal(42.5, SmcProtocol.DecodeTemperature(info, SmcProtocol.BytesOf(response, info.DataSize)));
    }

    [Fact]
    public void DecodesIntelFixedPointTemperature()
    {
        var info = new SmcKeyInfo(2, SmcProtocol.TypeSp78);

        Assert.Equal(47.5, SmcProtocol.DecodeTemperature(info, [0x2F, 0x80]));
    }

    [Fact]
    public void IgnoresNonTemperatureTypes()
    {
        var info = new SmcKeyInfo(4, SmcProtocol.FourCc("ioft"));

        Assert.False(SmcProtocol.IsTemperatureType(info));
        Assert.Null(SmcProtocol.DecodeTemperature(info, [1, 2, 3, 4]));
    }

    [Fact]
    public void DecodesKeyCountAsBigEndian()
    {
        var info = new SmcKeyInfo(4, SmcProtocol.TypeUInt32);

        Assert.Equal(1592u, SmcProtocol.DecodeUInt32(info, [0x00, 0x00, 0x06, 0x38]));
    }
}
