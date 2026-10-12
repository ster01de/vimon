namespace Vimon.Core;

/// <summary>USB link speeds, slowest first, so the slowest link of a chain is the minimum.</summary>
public enum UsbSpeed { Low, Full, High, Super, SuperPlus, SuperPlusBy2 }

/// <summary>USB speed codes and the names users find on boxes and cables (USB-IF naming since USB 3.2). Pure.</summary>
public static class UsbSpeeds
{
    /// <summary>macOS "USBSpeed" (tIOUSBHostConnectionSpeed): 1 full, 2 low, 3 high, 4 super, 5 super+, 6 super+ x2.</summary>
    public static UsbSpeed? FromHostConnectionSpeed(long code) => code switch
    {
        1 => UsbSpeed.Full,
        2 => UsbSpeed.Low,
        3 => UsbSpeed.High,
        4 => UsbSpeed.Super,
        5 => UsbSpeed.SuperPlus,
        6 => UsbSpeed.SuperPlusBy2,
        _ => null
    };

    /// <summary>macOS "Device Speed" (older IOUSBFamily numbering): 0 low, 1 full, 2 high, 3 super, 4 super+, 5 super+ x2.</summary>
    public static UsbSpeed? FromDeviceSpeed(long code) => code switch
    {
        0 => UsbSpeed.Low,
        1 => UsbSpeed.Full,
        2 => UsbSpeed.High,
        3 => UsbSpeed.Super,
        4 => UsbSpeed.SuperPlus,
        5 => UsbSpeed.SuperPlusBy2,
        _ => null
    };

    public static string Name(UsbSpeed speed) => speed switch
    {
        UsbSpeed.Low => "USB 1.1 Low Speed",
        UsbSpeed.Full => "USB 1.1",
        UsbSpeed.High => "USB 2.0",
        UsbSpeed.Super => "USB 3.2 Gen 1",
        UsbSpeed.SuperPlus => "USB 3.2 Gen 2",
        _ => "USB 3.2 Gen 2x2"
    };

    public static string Rate(UsbSpeed speed) => speed switch
    {
        UsbSpeed.Low => "1.5 Mb/s",
        UsbSpeed.Full => "12 Mb/s",
        UsbSpeed.High => "480 Mb/s",
        UsbSpeed.Super => "5 Gb/s",
        UsbSpeed.SuperPlus => "10 Gb/s",
        _ => "20 Gb/s"
    };

    /// <returns>For example <c>USB 3.2 Gen 1 · 5 Gb/s</c>.</returns>
    public static string Describe(UsbSpeed speed) => $"{Name(speed)} · {Rate(speed)}";

    /// <summary>USB 2.0 and slower limit even a hard disk (~40 MB/s in practice).</summary>
    public static bool IsSlow(UsbSpeed speed) => speed <= UsbSpeed.High;
}
