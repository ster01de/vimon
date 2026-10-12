using Vimon.Core;

namespace Vimon.Core.Tests;

public class UsbLinkTests
{
    // Seen on an M2: a USB 2.0 hub reports USBSpeed 3 / Device Speed 2, a USB 3 drive USBSpeed 4 / Device Speed 3.
    [Theory]
    [InlineData(3L, 2L, "High")]
    [InlineData(4L, 3L, "Super")]
    [InlineData(5L, 4L, "SuperPlus")]
    [InlineData(6L, 5L, "SuperPlusBy2")]
    public void ReadsBothMacOSSpeedCodes(long hostCode, long deviceCode, string expected)
    {
        Assert.Equal(Enum.Parse<UsbSpeed>(expected), UsbSpeeds.FromHostConnectionSpeed(hostCode));
        Assert.Equal(Enum.Parse<UsbSpeed>(expected), UsbSpeeds.FromDeviceSpeed(deviceCode));
    }

    [Fact]
    public void IgnoresUnknownCodes()
    {
        Assert.Null(UsbSpeeds.FromHostConnectionSpeed(0));
        Assert.Null(UsbSpeeds.FromDeviceSpeed(99));
    }

    [Theory]
    [InlineData("High", "USB 2.0 · 480 Mb/s")]
    [InlineData("Super", "USB 3.2 Gen 1 · 5 Gb/s")]
    [InlineData("SuperPlus", "USB 3.2 Gen 2 · 10 Gb/s")]
    [InlineData("SuperPlusBy2", "USB 3.2 Gen 2x2 · 20 Gb/s")]
    public void UsesTheNamesFromBoxesAndCables(string speed, string expected) =>
        Assert.Equal(expected, UsbSpeeds.Describe(Enum.Parse<UsbSpeed>(speed)));

    [Fact]
    public void OrdersSpeedsSoTheSlowestLinkIsTheMinimum()
    {
        Assert.True(UsbSpeed.High < UsbSpeed.Super);
        Assert.True(UsbSpeeds.IsSlow(UsbSpeed.High));
        Assert.False(UsbSpeeds.IsSlow(UsbSpeed.Super));
    }

    [Fact]
    public void PointsOutAnExternalDriveOnAUsb2Link()
    {
        var snapshot = new SystemSnapshot(DateTimeOffset.UnixEpoch, null, [], new MemoryMetrics(4, 16, 25, 5, 20),
        [
            new StorageMetrics("TOSHIBA External USB 3.0", null) { IsExternal = true, UsbLink = UsbSpeed.High },
            new StorageMetrics("Seagate Expansion", null) { IsExternal = true, UsbLink = UsbSpeed.Super }
        ]);

        var finding = Assert.Single(DiagnosticsEngine.Evaluate(snapshot));

        Assert.Equal(HealthLevel.Info, finding.Level);
        Assert.Equal("USB_LINK", finding.Code);
        Assert.StartsWith("TOSHIBA External USB 3.0: USB 2.0 · 480 Mb/s", finding.Message);
    }
}
