using System.Runtime.InteropServices;
using System.Text;
using static Vimon.Platform.MacOS.MacNative;

namespace Vimon.Platform.MacOS;

/// <summary>Reading IORegistry properties and CoreFoundation values, shared by the IOKit readers.</summary>
internal static class MacRegistry
{
    public const string ServicePlane = "IOService";

    private const int MaxNameBytes = 256;

    /// <summary>Calls <paramref name="visit"/> for every service of a class and releases each entry.</summary>
    public static void ForEachService(string className, Action<uint> visit)
    {
        IntPtr matching = IOServiceMatching(className);

        // IOServiceGetMatchingServices consumes the matching dictionary, also on failure.
        if (matching == IntPtr.Zero || IOServiceGetMatchingServices(0, matching, out uint iterator) != KernSuccess)
            return;

        try
        {
            for (uint entry = IOIteratorNext(iterator); entry != 0; entry = IOIteratorNext(iterator))
            {
                try { visit(entry); }
                finally { IOObjectRelease(entry); }
            }
        }
        finally
        {
            IOObjectRelease(iterator);
        }
    }

    /// <returns>An owned CF object (caller releases), or <see cref="IntPtr.Zero"/>.</returns>
    public static IntPtr CopyProperty(uint entry, string key)
    {
        IntPtr cfKey = CFStringCreateWithCString(IntPtr.Zero, key, CFStringEncodingUtf8);

        if (cfKey == IntPtr.Zero)
            return IntPtr.Zero;

        try
        {
            return IORegistryEntryCreateCFProperty(entry, cfKey, IntPtr.Zero, 0);
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    /// <summary>Copies a dictionary property and hands it to <paramref name="read"/>; default when it is missing.</summary>
    public static T? WithDictionary<T>(uint entry, string key, Func<IntPtr, T> read)
    {
        IntPtr value = CopyProperty(entry, key);

        if (value == IntPtr.Zero)
            return default;

        try
        {
            return CFGetTypeID(value) == CFDictionaryGetTypeID() ? read(value) : default;
        }
        finally
        {
            CFRelease(value);
        }
    }

    public static string? ReadStringProperty(uint entry, string key)
    {
        IntPtr value = CopyProperty(entry, key);

        if (value == IntPtr.Zero)
            return null;

        try
        {
            return ToText(value);
        }
        finally
        {
            CFRelease(value);
        }
    }

    public static long? ReadInt64Property(uint entry, string key)
    {
        IntPtr value = CopyProperty(entry, key);

        if (value == IntPtr.Zero)
            return null;

        try
        {
            return CFGetTypeID(value) == CFNumberGetTypeID() && CFNumberGetValue(value, CFNumberSInt64Type, out long number)
                ? number
                : null;
        }
        finally
        {
            CFRelease(value);
        }
    }

    public static double? DictionaryNumber(IntPtr dictionary, string key)
    {
        IntPtr value = DictionaryValue(dictionary, key);

        if (value == IntPtr.Zero || CFGetTypeID(value) != CFNumberGetTypeID())
            return null;

        // A false result only signals a lossy conversion; the value is still the closest double.
        CFNumberGetValue(value, CFNumberDoubleType, out double number);
        return number;
    }

    /// <summary>Byte counters exceed what a double holds exactly, so they are read as 64-bit integers.</summary>
    public static long? DictionaryInt64(IntPtr dictionary, string key)
    {
        IntPtr value = DictionaryValue(dictionary, key);

        if (value == IntPtr.Zero || CFGetTypeID(value) != CFNumberGetTypeID())
            return null;

        return CFNumberGetValue(value, CFNumberSInt64Type, out long number) ? number : null;
    }

    public static string? DictionaryString(IntPtr dictionary, string key)
    {
        IntPtr value = DictionaryValue(dictionary, key);
        return value == IntPtr.Zero ? null : ToText(value);
    }

    /// <returns>A borrowed reference owned by the dictionary (must not be released), or zero.</returns>
    private static IntPtr DictionaryValue(IntPtr dictionary, string key)
    {
        IntPtr cfKey = CFStringCreateWithCString(IntPtr.Zero, key, CFStringEncodingUtf8);

        if (cfKey == IntPtr.Zero)
            return IntPtr.Zero;

        try
        {
            return CFDictionaryGetValue(dictionary, cfKey);
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    /// <summary>CFString or CFData (some drivers store names as bytes) as trimmed text.</summary>
    private static string? ToText(IntPtr value)
    {
        nuint type = CFGetTypeID(value);

        if (type == CFStringGetTypeID())
        {
            var buffer = new byte[MaxNameBytes];
            return CFStringGetCString(value, buffer, buffer.Length, CFStringEncodingUtf8)
                ? DecodeNullTerminated(buffer)
                : null;
        }

        if (type == CFDataGetTypeID())
        {
            int length = (int)Math.Min(CFDataGetLength(value), MaxNameBytes);

            if (length <= 0)
                return null;

            var bytes = new byte[length];
            Marshal.Copy(CFDataGetBytePtr(value), bytes, 0, length);
            return DecodeNullTerminated(bytes);
        }

        return null;
    }

    private static string? DecodeNullTerminated(byte[] bytes)
    {
        int end = Array.IndexOf(bytes, (byte)0);
        string text = Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end).Trim();
        return text.Length == 0 ? null : text;
    }
}
