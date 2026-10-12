namespace Vimon.Cli.Tests;

public class MetricFormatTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 5)]
    [InlineData(100, 10)]
    [InlineData(250, 10)]
    [InlineData(-5, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    public void FillsBarCellsAndTreatsNonFiniteValuesAsEmpty(double percent, int expected) =>
        Assert.Equal(expected, MetricFormat.FilledCells(percent, cells: 10));

    [Theory]
    [InlineData("short", 10, "short")]
    [InlineData("exactly10!", 10, "exactly10!")]
    [InlineData("much too long", 8, "much to…")]
    [InlineData("anything", 0, "")]
    public void TruncatesWithAnEllipsis(string text, int width, string expected) =>
        Assert.Equal(expected, MetricFormat.Truncate(text, width));
}
