using System.Globalization;

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

    // Expectations are written with '.', the separator of the culture running the tests replaces it.
    private static string Localised(string text) =>
        text.Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

    [Theory]
    [InlineData(512.0, "512 B/s", "512B")]
    [InlineData(310_000.0, "310 KB/s", "310K")]
    [InlineData(999_600.0, "1.0 MB/s", "1.0M")]
    [InlineData(1_234_567.0, "1.2 MB/s", "1.2M")]
    [InlineData(12_345_678.0, "12 MB/s", "12M")]
    [InlineData(2_500_000_000.0, "2.5 GB/s", "2.5G")]
    public void FormatsThroughputInDecimalUnits(double bytesPerSec, string full, string brief)
    {
        Assert.Equal(Localised(full), MetricFormat.Throughput(bytesPerSec));
        Assert.Equal(Localised(brief), MetricFormat.ShortThroughput(bytesPerSec));
    }

    [Fact]
    public void FormatsMemoryInBinaryUnits()
    {
        Assert.Equal("300 MB", MetricFormat.Memory(300UL << 20));
        Assert.Equal($"{4.5:0.0} GB", MetricFormat.Memory((ulong)(4.5 * (1UL << 30))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void LeavesMissingOrInvalidThroughputOut(double? bytesPerSec)
    {
        Assert.Null(MetricFormat.Throughput(bytesPerSec));
        Assert.Null(MetricFormat.ShortThroughput(bytesPerSec));
    }

    [Theory]
    [InlineData("short", 10, "short")]
    [InlineData("exactly10!", 10, "exactly10!")]
    [InlineData("much too long", 8, "much to…")]
    [InlineData("anything", 0, "")]
    [InlineData("ab\U0001F600cd", 4, "ab…")]
    public void TruncatesWithAnEllipsis(string text, int width, string expected) =>
        Assert.Equal(expected, MetricFormat.Truncate(text, width));
}
