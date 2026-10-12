using System.Buffers.Binary;

namespace Vimon.Platform.MacOS;

/// <summary>
/// Parses <c>proc_pid_rusage(RUSAGE_INFO_V2)</c>: 160 bytes with the bytes a process read from and
/// wrote to storage at offsets 144 and 152 (checked with offsetof on macOS 13; no pointers, so arm64
/// and x64 share the layout). Writes count once the cache is flushed to the drive. Pure.
/// </summary>
internal static class MacRusage
{
    public const int Flavor = 2; // RUSAGE_INFO_V2
    public const int Length = 160;

    private const int DiskReadOffset = 144;
    private const int DiskWriteOffset = 152;

    public static (ulong Read, ulong Written) Parse(ReadOnlySpan<byte> info) => (
        BinaryPrimitives.ReadUInt64LittleEndian(info[DiskReadOffset..]),
        BinaryPrimitives.ReadUInt64LittleEndian(info[DiskWriteOffset..]));
}
