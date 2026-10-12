using Vimon.Core;

namespace Vimon.Platform.MacOS.Tests;

public class MacDisksTests
{
    private const int WriteBytes = 4 * 1024 * 1024;
    private const int SampleIntervalMs = 300;

    [Theory]
    [InlineData("TOSHIBA", "External USB 3.0", "TOSHIBA External USB 3.0")]
    [InlineData("", "APPLE SSD AP0256Z", "APPLE SSD AP0256Z")]
    [InlineData("Samsung", "Samsung Portable SSD T7", "Samsung Portable SSD T7")]
    [InlineData("WD", "", "WD")]
    [InlineData(null, null, MacDiskNames.UnknownDrive)]
    public void NamesDrivesFromVendorAndProduct(string? vendor, string? product, string expected) =>
        Assert.Equal(expected, MacDiskNames.Describe(vendor, product, "USB", "External").Name);

    [Theory]
    [InlineData("External", true, false)]
    [InlineData("Internal", false, false)]
    [InlineData("File", null, true)]
    [InlineData(null, null, false)]
    public void TellsExternalDrivesAndDiskImagesApart(string? location, bool? isExternal, bool isDiskImage)
    {
        var topology = MacDiskNames.Describe("v", "p", "USB", location);

        Assert.Equal(isExternal, topology.IsExternal);
        Assert.Equal(isDiskImage, topology.IsDiskImage);
    }

    [Fact]
    public void CarriesRatesAndAttachmentIntoTheModel()
    {
        var drive = new MacDiskTopology("TOSHIBA External USB 3.0", "USB", true, false);

        var metrics = MacDisks.ToMetrics(drive, new InterfaceRate("42", 1000, 2000));

        Assert.Equal("TOSHIBA External USB 3.0", metrics.Name);
        Assert.Equal(1000, metrics.ReadBytesPerSec);
        Assert.Equal(2000, metrics.WriteBytesPerSec);
        Assert.Equal("USB", metrics.Interconnect);
        Assert.True(metrics.IsExternal);
    }

    [MacOSFact]
    public void ReadsDriveCountersOfThisMac()
    {
        var disks = new MacDisks();
        var first = disks.Read();

        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, new byte[WriteBytes]);
            using (var stream = new FileStream(path, FileMode.Open)) stream.Flush(flushToDisk: true);
            Thread.Sleep(SampleIntervalMs);

            var second = disks.Read();

            Assert.All(first, d => Assert.Null(d.ReadBytesPerSec));
            Assert.All(second, d =>
            {
                Assert.NotEmpty(d.Name);
                Assert.True(d.ReadBytesPerSec is null or >= 0, d.Name);
                Assert.True(d.WriteBytesPerSec is null or >= 0, d.Name);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }
}
