namespace Vimon.Cli.Tests;

public class TerminalFrameTests
{
    private static string Frame(int lineCount, int width = 10) =>
        string.Concat(Enumerable.Range(0, lineCount)
            .Select(i => $"{i}".PadRight(width, 'x') + Environment.NewLine));

    private static string[] Lines(FittedFrame fitted) =>
        fitted.Text.Split(Environment.NewLine)[..^1];

    [Fact]
    public void KeepsFrameThatFitsTheWindow()
    {
        string frame = Frame(5);

        var fitted = TerminalFrame.Fit(frame, windowHeight: 24, previousLineCount: 0);

        Assert.Equal(frame, fitted.Text);
        Assert.Equal(5, fitted.LineCount);
    }

    [Fact]
    public void TruncatesTallFrameAndLeavesLastRowFree()
    {
        var fitted = TerminalFrame.Fit(Frame(10, width: 60), windowHeight: 6, previousLineCount: 0);

        var lines = Lines(fitted);
        Assert.Equal(5, fitted.LineCount);
        Assert.Equal(5, lines.Length);
        Assert.StartsWith("3", lines[3]);
        Assert.StartsWith("… 6 more lines", lines[4]);
        Assert.Equal(60, lines[4].Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void KeepsWholeFrameWhenWindowHeightIsUnknown(int windowHeight)
    {
        // Some pseudo terminals report 0 rows; truncating to that would leave only the hint.
        string frame = Frame(40);

        var fitted = TerminalFrame.Fit(frame, windowHeight, previousLineCount: 0);

        Assert.Equal(frame, fitted.Text);
        Assert.Equal(40, fitted.LineCount);
    }

    [Fact]
    public void BlanksRowsLeftOverFromTallerPreviousFrame()
    {
        var fitted = TerminalFrame.Fit(Frame(3), windowHeight: 24, previousLineCount: 5);

        var lines = Lines(fitted);
        Assert.Equal(3, fitted.LineCount);
        Assert.Equal(5, lines.Length);
        Assert.Equal(new string(' ', 10), lines[3]);
        Assert.Equal(new string(' ', 10), lines[4]);
    }

    [Fact]
    public void AcceptsWindowsLineEndings()
    {
        var fitted = TerminalFrame.Fit("ab\r\ncd\r\n", windowHeight: 24, previousLineCount: 0);

        Assert.Equal(new[] { "ab", "cd" }, Lines(fitted));
    }
}
