namespace Vimon.Cli.Tests;

public class TerminalScreenTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void UsesAlternateScreenOnlyOnInteractiveUnixTerminals(bool isWindows, bool isRedirected, bool expected) =>
        Assert.Equal(expected, TerminalScreen.ShouldUseAlternateScreen(isWindows, isRedirected));

    [Fact]
    public void EmitsTheStandardXtermSequences()
    {
        Assert.Equal("\u001b[?1049h", TerminalScreen.EnterAlternateScreen);
        Assert.Equal("\u001b[?1049l", TerminalScreen.LeaveAlternateScreen);
    }
}
