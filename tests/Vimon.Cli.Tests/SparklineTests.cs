namespace Vimon.Cli.Tests;

public class SparklineTests
{
    [Fact]
    public void MapsValuesOntoEightBlockLevels() =>
        Assert.Equal("▁▅█", Sparkline.Render([0, 50, 100], min: 0, max: 100));

    [Fact]
    public void ClampsValuesOutsideTheScale() =>
        Assert.Equal("▁█", Sparkline.Render([-10, 250], min: 0, max: 100));

    [Fact]
    public void ReturnsEmptyForNoValuesOrAnInvalidScale()
    {
        Assert.Equal("", Sparkline.Render([], min: 0, max: 100));
        Assert.Equal("", Sparkline.Render([50], min: 100, max: 100));
    }

    [Theory]
    [InlineData("", 5, "     ")]
    [InlineData("▁▅█", 5, "  ▁▅█")]
    [InlineData("▁▂▃▄▅▆▇", 5, "▃▄▅▆▇")]
    public void KeepsTheNewestBlocksAtAFixedLength(string blocks, int length, string expected) =>
        Assert.Equal(expected, Sparkline.Fixed(blocks, length));

    [Fact]
    public void KeepsBlankPaddingInTheAsciiSet() =>
        Assert.Equal("  _#", Glyphs.Safe.Spark(Sparkline.Fixed("▁█", 4)));
}
