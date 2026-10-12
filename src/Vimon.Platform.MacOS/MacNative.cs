using System.Runtime.InteropServices;

namespace Vimon.Platform.MacOS;

/// <summary>P/Invoke declarations for the public macOS system APIs used by the backend.</summary>
internal static class MacNative
{
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";
    private const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";

    public const string Foundation = "/System/Library/Frameworks/Foundation.framework/Foundation";

    public const int KernSuccess = 0;
    public const int HostCpuLoadInfo = 3;
    public const uint HostCpuLoadInfoCount = 4;
    public const int HostVmInfo64 = 4;
    public const uint HostVmInfo64Count = 38;

    public const uint CFStringEncodingUtf8 = 0x08000100;
    public const int CFNumberDoubleType = 13;
    public const int CFNumberSInt64Type = 4;

    // libSystem: sysctl and Mach host statistics

    [DllImport(LibSystem)]
    public static extern int sysctlbyname(string name, IntPtr oldp, ref nuint oldlenp, IntPtr newp, nuint newlen);

    /// <remarks>A null <paramref name="oldp"/> asks for the size only.</remarks>
    [DllImport(LibSystem, SetLastError = true)]
    public static extern int sysctl(int[] name, uint namelen, byte[]? oldp, ref nuint oldlenp, IntPtr newp, nuint newlen);

    /// <returns>The number of pids written, or the number of processes when <paramref name="buffer"/> is null.</returns>
    [DllImport(LibSystem, SetLastError = true)]
    public static extern int proc_listallpids(int[]? buffer, int bufferBytes);

    /// <returns>The number of bytes written; 0 for processes of other users (EPERM).</returns>
    [DllImport(LibSystem, SetLastError = true)]
    public static extern int proc_pidinfo(int pid, int flavor, ulong arg, byte[] buffer, int bufferBytes);

    [DllImport(LibSystem)]
    public static extern int mach_timebase_info(out MachTimebaseInfo info);

    [DllImport(LibSystem)]
    public static extern uint mach_host_self();

    [DllImport(LibSystem)]
    public static extern int host_page_size(uint host, out nuint pageSize);

    [DllImport(LibSystem)]
    public static extern int host_statistics(uint host, int flavor, out CpuLoadInfo info, ref uint count);

    [DllImport(LibSystem)]
    public static extern int host_statistics64(uint host, int flavor, out VmStatistics64 info, ref uint count);

    // IOKit registry

    [DllImport(IOKit)]
    public static extern IntPtr IOServiceMatching(string name);

    /// <remarks>Consumes one reference to <paramref name="matching"/>.</remarks>
    [DllImport(IOKit)]
    public static extern int IOServiceGetMatchingServices(uint mainPort, IntPtr matching, out uint iterator);

    [DllImport(IOKit)]
    public static extern uint IOIteratorNext(uint iterator);

    [DllImport(IOKit)]
    public static extern int IOObjectRelease(uint obj);

    [DllImport(IOKit)]
    public static extern int IORegistryEntryGetParentEntry(uint entry, string plane, out uint parent);

    /// <summary>A system-wide ID that stays the same while the entry exists.</summary>
    [DllImport(IOKit)]
    public static extern int IORegistryEntryGetRegistryEntryID(uint entry, out ulong entryId);

    [DllImport(IOKit)]
    public static extern IntPtr IORegistryEntryCreateCFProperty(uint entry, IntPtr key, IntPtr allocator, uint options);

    /// <remarks>Consumes one reference to <paramref name="matching"/>.</remarks>
    [DllImport(IOKit)]
    public static extern uint IOServiceGetMatchingService(uint mainPort, IntPtr matching);

    [DllImport(IOKit)]
    public static extern int IOServiceOpen(uint service, uint owningTask, uint type, out uint connect);

    [DllImport(IOKit)]
    public static extern int IOServiceClose(uint connect);

    /// <remarks>Byte arrays are pinned, so the driver writes straight into <paramref name="output"/>.</remarks>
    [DllImport(IOKit)]
    public static extern int IOConnectCallStructMethod(
        uint connection, uint selector, byte[] input, nuint inputSize, byte[] output, ref nuint outputSize);

    /// <summary>mach_task_self() is a macro over the exported variable mach_task_self_.</summary>
    public static uint MachTaskSelf()
    {
        IntPtr library = NativeLibrary.Load(LibSystem);
        return unchecked((uint)Marshal.ReadInt32(NativeLibrary.GetExport(library, "mach_task_self_")));
    }

    // CoreFoundation

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);

    [DllImport(CoreFoundation)]
    public static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundation)]
    public static extern nuint CFGetTypeID(IntPtr cf);

    [DllImport(CoreFoundation)]
    public static extern nuint CFStringGetTypeID();

    [DllImport(CoreFoundation)]
    public static extern nuint CFNumberGetTypeID();

    [DllImport(CoreFoundation)]
    public static extern nuint CFDataGetTypeID();

    [DllImport(CoreFoundation)]
    public static extern nuint CFDictionaryGetTypeID();

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, int type, out double value);

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, int type, out long value);

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFStringGetCString(IntPtr value, byte[] buffer, nint bufferSize, uint encoding);

    [DllImport(CoreFoundation)]
    public static extern nint CFDataGetLength(IntPtr data);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFDataGetBytePtr(IntPtr data);

    // Objective-C runtime

    [DllImport(LibObjC)]
    public static extern IntPtr objc_getClass(string name);

    [DllImport(LibObjC)]
    public static extern IntPtr sel_registerName(string name);

    [DllImport(LibObjC)]
    public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);
}

/// <summary>host_cpu_load_info: cumulative ticks per CPU state, in CPU_STATE_* order.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CpuLoadInfo
{
    public uint User;
    public uint System;
    public uint Idle;
    public uint Nice;

    public CpuLoadInfo(uint user, uint system, uint idle, uint nice)
    {
        User = user;
        System = system;
        Idle = idle;
        Nice = nice;
    }
}

/// <summary>vm_statistics64 from &lt;mach/vm_statistics.h&gt;. Field order and sizes must match exactly.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VmStatistics64
{
    public uint FreeCount;
    public uint ActiveCount;
    public uint InactiveCount;
    public uint WireCount;
    public ulong ZeroFillCount;
    public ulong Reactivations;
    public ulong Pageins;
    public ulong Pageouts;
    public ulong Faults;
    public ulong CowFaults;
    public ulong Lookups;
    public ulong Hits;
    public ulong Purges;
    public uint PurgeableCount;
    public uint SpeculativeCount;
    public ulong Decompressions;
    public ulong Compressions;
    public ulong Swapins;
    public ulong Swapouts;
    public uint CompressorPageCount;
    public uint ThrottledCount;
    public uint ExternalPageCount;
    public uint InternalPageCount;
    public ulong TotalUncompressedPagesInCompressor;
}

/// <summary>mach_timebase_info_data_t: nanoseconds = Mach ticks * Numer / Denom (125/3 on Apple Silicon).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MachTimebaseInfo
{
    public uint Numer;
    public uint Denom;
}

/// <summary>xsw_usage from &lt;sys/sysctl.h&gt; (vm.swapusage).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SwapUsage
{
    public ulong Total;
    public ulong Available;
    public ulong Used;
    public uint PageSize;
    public int Encrypted;
}
