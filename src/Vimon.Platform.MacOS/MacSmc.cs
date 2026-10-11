using static Vimon.Platform.MacOS.MacNative;

namespace Vimon.Platform.MacOS;

/// <summary>Read-only connection to the AppleSMC user client. Needs no root.</summary>
internal sealed class MacSmc : IDisposable
{
    private const uint KernelIndexSmc = 2;
    private const uint MainPortDefault = 0;

    private uint _connection;

    private MacSmc(uint connection) => _connection = connection;

    /// <returns>An open connection, or null when the machine exposes no AppleSMC (e.g. some VMs).</returns>
    public static MacSmc? TryOpen()
    {
        IntPtr matching = IOServiceMatching("AppleSMC");

        if (matching == IntPtr.Zero)
            return null;

        uint service = IOServiceGetMatchingService(MainPortDefault, matching);

        if (service == 0)
            return null;

        try
        {
            return IOServiceOpen(service, MachTaskSelf(), 0, out uint connection) == KernSuccess && connection != 0
                ? new MacSmc(connection)
                : null;
        }
        finally
        {
            IOObjectRelease(service);
        }
    }

    public uint? ReadKeyCount()
    {
        uint key = SmcProtocol.FourCc("#KEY");

        return ReadKeyInfo(key) is SmcKeyInfo info && ReadBytes(key, info) is byte[] bytes
            ? SmcProtocol.DecodeUInt32(info, bytes)
            : null;
    }

    public uint? KeyAt(uint index) =>
        Call(SmcProtocol.ReadIndexRequest(index)) is byte[] response
            ? SmcProtocol.KeyOf(response)
            : null;

    public SmcKeyInfo? ReadKeyInfo(uint key) =>
        Call(SmcProtocol.ReadKeyInfoRequest(key)) is byte[] response
            ? SmcProtocol.KeyInfoOf(response)
            : null;

    public byte[]? ReadBytes(uint key, SmcKeyInfo info) =>
        Call(SmcProtocol.ReadBytesRequest(key, info.DataSize)) is byte[] response
            ? SmcProtocol.BytesOf(response, info.DataSize)
            : null;

    /// <returns>The response, or null when the call or the SMC itself reports an error.</returns>
    private byte[]? Call(byte[] request)
    {
        ObjectDisposedException.ThrowIf(_connection == 0, this);

        var response = new byte[SmcProtocol.StructSize];
        nuint responseSize = (nuint)response.Length;

        int result = IOConnectCallStructMethod(
            _connection, KernelIndexSmc, request, (nuint)request.Length, response, ref responseSize);

        return result == KernSuccess && SmcProtocol.ResultOf(response) == SmcProtocol.ResultSuccess
            ? response
            : null;
    }

    public void Dispose()
    {
        if (_connection == 0)
            return;

        IOServiceClose(_connection);
        _connection = 0;
    }
}
