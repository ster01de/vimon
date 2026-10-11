using Vimon.Core;

namespace Vimon.Core.Tests;

public class NvidiaSmiTests
{
    private static NvidiaSmiGpu SmiGpu(string name, int widthCurrent) =>
        new(name, new PcieLink(4, widthCurrent, 4, 16));

    [Fact]
    public void ParsesEveryGpuLine()
    {
        const string csv = "NVIDIA GeForce RTX 3090, 4, 16, 4, 16\r\nNVIDIA GeForce RTX 3060, 3, 8, 4, 16\r\n";

        var gpus = NvidiaSmi.ParsePcieQuery(csv);

        Assert.Equal(2, gpus.Count);
        Assert.Equal(new NvidiaSmiGpu("NVIDIA GeForce RTX 3090", new PcieLink(4, 16, 4, 16)), gpus[0]);
        Assert.Equal(new NvidiaSmiGpu("NVIDIA GeForce RTX 3060", new PcieLink(3, 8, 4, 16)), gpus[1]);
    }

    [Fact]
    public void TreatsUnavailableValuesAsUnknown()
    {
        var gpu = Assert.Single(NvidiaSmi.ParsePcieQuery("Tesla T4, [N/A], [N/A], 3, 16"));

        Assert.Equal(new PcieLink(null, null, 3, 16), gpu.Pcie);
    }

    [Fact]
    public void KeepsCommasInsideGpuNames()
    {
        var gpu = Assert.Single(NvidiaSmi.ParsePcieQuery("Odd, Name, 4, 16, 4, 16"));

        Assert.Equal("Odd,Name", gpu.Name);
        Assert.Equal(new PcieLink(4, 16, 4, 16), gpu.Pcie);
    }

    [Theory]
    [InlineData("")]
    [InlineData("No devices were found")]
    [InlineData("4, 16, 4, 16")]
    public void SkipsMalformedOutput(string csv) =>
        Assert.Empty(NvidiaSmi.ParsePcieQuery(csv));

    [Fact]
    public void MatchesDifferentModelsByNameRegardlessOfOrder()
    {
        var smi = new[] { SmiGpu("NVIDIA GeForce RTX 3060", 8), SmiGpu("NVIDIA GeForce RTX 3090", 16) };

        var links = NvidiaSmi.MatchByName(["NVIDIA GeForce RTX 3090", "NVIDIA GeForce RTX 3060"], smi);

        Assert.Equal(new int?[] { 16, 8 }, links.Select(link => link.WidthCurrent).ToArray());
    }

    [Fact]
    public void MatchesNamesWithoutVendorPrefixAndIgnoresCase()
    {
        var smi = new[] { SmiGpu("GeForce RTX 3090", 16) };

        var link = Assert.Single(NvidiaSmi.MatchByName(["nvidia geforce rtx 3090"], smi));

        Assert.Equal(16, link.WidthCurrent);
    }

    [Fact]
    public void MatchesIdenticalModelsInOrder()
    {
        var smi = new[] { SmiGpu("NVIDIA GeForce RTX 3090", 16), SmiGpu("NVIDIA GeForce RTX 3090", 8) };

        var links = NvidiaSmi.MatchByName(["NVIDIA GeForce RTX 3090", "NVIDIA GeForce RTX 3090"], smi);

        Assert.Equal(new int?[] { 16, 8 }, links.Select(link => link.WidthCurrent).ToArray());
    }

    [Fact]
    public void ReturnsUnknownForGpusWithoutNvidiaSmiRow()
    {
        var smi = new[] { SmiGpu("NVIDIA GeForce RTX 3090", 16) };

        var links = NvidiaSmi.MatchByName(["NVIDIA GeForce RTX 3090", "NVIDIA GeForce RTX 3090", "NVIDIA RTX A4000"], smi);

        Assert.Equal(new int?[] { 16, null, null }, links.Select(link => link.WidthCurrent).ToArray());
        Assert.Equal(PcieLink.Unknown, links[2]);
    }
}
