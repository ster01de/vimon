using System.Runtime.InteropServices;

namespace Vimon.Platform.MacOS;

internal static class MacSysctl
{
    public static string? ReadString(string name)
    {
        nuint length = 0;

        if (MacNative.sysctlbyname(name, IntPtr.Zero, ref length, IntPtr.Zero, 0) != 0 || length == 0)
            return null;

        IntPtr buffer = Marshal.AllocHGlobal(checked((int)length));

        try
        {
            if (MacNative.sysctlbyname(name, buffer, ref length, IntPtr.Zero, 0) != 0)
                return null;

            string? value = Marshal.PtrToStringUTF8(buffer)?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Reads a fixed-size value. Returns null when the key is missing or its size differs.</summary>
    public static T? Read<T>(string name) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        IntPtr buffer = Marshal.AllocHGlobal(size);

        try
        {
            nuint length = (nuint)size;

            if (MacNative.sysctlbyname(name, buffer, ref length, IntPtr.Zero, 0) != 0 || length != (nuint)size)
                return null;

            return Marshal.PtrToStructure<T>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
