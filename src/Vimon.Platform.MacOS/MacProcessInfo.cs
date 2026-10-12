using System.Buffers.Binary;
using System.Text;
using Vimon.Core;

namespace Vimon.Platform.MacOS;

/// <summary>
/// Parses <c>proc_pidinfo(PROC_PIDTASKALLINFO)</c>: a 136-byte <c>proc_bsdinfo</c> followed by a
/// 96-byte <c>proc_taskinfo</c>. Offsets were checked with offsetof on macOS 13; the structs have
/// no pointers, so arm64 and x64 share the layout. Pure.
/// </summary>
internal static class MacProcessInfo
{
    public const int Flavor = 2; // PROC_PIDTASKALLINFO
    public const int Length = 232;

    private const int CommOffset = 48;
    private const int CommLength = 16;
    private const int NameOffset = 64;
    private const int NameLength = 32;
    private const int StartSecondsOffset = 120;
    private const int StartMicrosecondsOffset = 128;
    private const int TaskInfoOffset = 136;
    private const int ResidentOffset = TaskInfoOffset + 8;
    private const int UserTimeOffset = TaskInfoOffset + 16;
    private const int SystemTimeOffset = TaskInfoOffset + 24;
    private const long MicrosecondsPerSecond = 1_000_000;
    private const double NanosecondsPerTick = 100;

    /// <param name="nanosecondsPerMachTick">From mach_timebase_info; CPU times are counted in Mach ticks.</param>
    public static ProcessTimes Parse(int pid, ReadOnlySpan<byte> info, double nanosecondsPerMachTick)
    {
        long startSeconds = BinaryPrimitives.ReadInt64LittleEndian(info[StartSecondsOffset..]);
        long startMicros = BinaryPrimitives.ReadInt64LittleEndian(info[StartMicrosecondsOffset..]);
        ulong machTicks = BinaryPrimitives.ReadUInt64LittleEndian(info[UserTimeOffset..])
                          + BinaryPrimitives.ReadUInt64LittleEndian(info[SystemTimeOffset..]);

        // pbi_name holds the full name (up to 32 bytes); pbi_comm only the first 16.
        string name = ReadCString(info.Slice(NameOffset, NameLength));
        if (name.Length == 0)
            name = ReadCString(info.Slice(CommOffset, CommLength));

        return new ProcessTimes(
            new ProcessKey(pid, startSeconds * MicrosecondsPerSecond + startMicros),
            ProcessName.Clean(name, pid),
            // Rounded: 125/3 is not exact in floating point, and truncating would lose a tick.
            TimeSpan.FromTicks((long)Math.Round(machTicks * nanosecondsPerMachTick / NanosecondsPerTick)),
            BinaryPrimitives.ReadUInt64LittleEndian(info[ResidentOffset..]));
    }

    private static string ReadCString(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? bytes : bytes[..end]);
    }
}
