using Vimon.Core;

namespace Vimon.Platform.MacOS.Tests;

public class MacGpuReaderTests
{
    [Theory]
    [InlineData("Apple M2 Pro", Vendor.Apple)]
    [InlineData("AMD Radeon Pro 5500M", Vendor.Amd)]
    [InlineData("Intel UHD Graphics 630", Vendor.Intel)]
    [InlineData("NVIDIA GeForce GT 750M", Vendor.Nvidia)]
    [InlineData("GPU 1", Vendor.Unknown)]
    public void DetectsVendorFromModelName(string name, Vendor expected) =>
        Assert.Equal(expected, MacGpuReader.VendorOf(name));
}
