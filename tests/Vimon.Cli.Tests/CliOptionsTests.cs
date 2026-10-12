namespace Vimon.Cli.Tests;

public class CliOptionsTests
{
    [Fact]
    public void DefaultsToTheFullLiveView()
    {
        var options = CliOptions.Parse([]);

        Assert.Equal(new CliOptions(Once: false, ViewMode.Full, HideMissing: false), options);
    }

    [Fact]
    public void ReadsEveryFlag()
    {
        var options = CliOptions.Parse(["--once", "--compact", "--hide-missing"]);

        Assert.Equal(new CliOptions(Once: true, ViewMode.Compact, HideMissing: true), options);
    }

    [Fact]
    public void ReadsAscii()
    {
        Assert.True(CliOptions.Parse(["--ascii"]).Ascii);
        Assert.False(CliOptions.Parse([]).Ascii);
    }

    [Fact]
    public void ReadsNoProcesses()
    {
        Assert.True(CliOptions.Parse(["--no-processes"]).NoProcesses);
        Assert.False(CliOptions.Parse([]).NoProcesses);
    }

    [Fact]
    public void WidgetWinsOverCompact()
    {
        Assert.Equal(ViewMode.Widget, CliOptions.Parse(["--compact", "--widget"]).Mode);
    }

    [Fact]
    public void RejectsUnknownOptionsWithAHelpfulMessage()
    {
        var error = Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--widgets"]));

        Assert.Contains("--widgets", error.Message);
        Assert.Contains("--widget", error.Message.Replace("--widgets", ""));
    }

    [Theory]
    [InlineData("--theme mono")]
    [InlineData("--theme=mono")]
    [InlineData("--theme MONO")]
    public void ReadsTheThemeInBothForms(string commandLine) =>
        Assert.Same(Theme.Mono, CliOptions.Parse(commandLine.Split(' ')).Theme);

    [Fact]
    public void LeavesTheThemeUnsetByDefaultAndTakesTheLastOne()
    {
        Assert.Null(CliOptions.Parse(["--once"]).Theme);
        Assert.Same(Theme.HighContrast, CliOptions.Parse(["--theme", "mono", "--theme=high-contrast"]).Theme);
    }

    [Theory]
    [InlineData("--theme")]
    [InlineData("--theme --once")]
    [InlineData("--theme=")]
    public void RejectsAMissingThemeName(string commandLine)
    {
        var error = Assert.Throws<ArgumentException>(() => CliOptions.Parse(commandLine.Split(' ')));

        Assert.Contains("needs a value", error.Message);
        Assert.Contains("high-contrast", error.Message);
    }

    [Fact]
    public void RejectsAnUnknownThemeAndListsTheValidOnes()
    {
        var error = Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--theme", "solarized"]));

        Assert.Contains("unknown theme 'solarized'", error.Message);
        Assert.Contains("default, mono, high-contrast", error.Message);
    }

    [Theory]
    [InlineData("Full", "Compact")]
    [InlineData("Compact", "Full")]
    [InlineData("Widget", "Widget")]
    public void CompactKeySwitchesFullAndCompactButNeverLeavesTheWidget(string current, string expected) =>
        Assert.Equal(Enum.Parse<ViewMode>(expected), CliOptions.ToggleCompact(Enum.Parse<ViewMode>(current)));
}
