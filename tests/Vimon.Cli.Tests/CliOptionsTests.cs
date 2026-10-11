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
    [InlineData("Full", "Compact")]
    [InlineData("Compact", "Full")]
    [InlineData("Widget", "Widget")]
    public void CompactKeySwitchesFullAndCompactButNeverLeavesTheWidget(string current, string expected) =>
        Assert.Equal(Enum.Parse<ViewMode>(expected), CliOptions.ToggleCompact(Enum.Parse<ViewMode>(current)));
}
