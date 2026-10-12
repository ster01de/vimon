using Spectre.Console;
using Vimon.Core;

namespace Vimon.Cli.Tests;

public class ThemeTests
{
    private static IEnumerable<string> Roles(Theme theme) =>
        [theme.Good, theme.Info, theme.Warning, theme.Critical, theme.Muted, theme.Accent];

    public static TheoryData<string> ThemeNames => new(Theme.All.Select(theme => theme.Name).ToArray());

    [Fact]
    public void FindsThemesIgnoringCase()
    {
        Assert.Same(Theme.HighContrast, Theme.Find("High-Contrast"));
        Assert.Null(Theme.Find("solarized"));
    }

    [Fact]
    public void HasUniqueNames()
    {
        Assert.Equal(Theme.All.Count, Theme.All.Select(theme => theme.Name).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void UsesOnlyValidSpectreStyles(string name)
    {
        var theme = Theme.Find(name)!;

        foreach (string role in Roles(theme))
            Assert.True(Style.TryParse(role, out _), $"{name}: '{role}' is not a Spectre style");
    }

    [Fact]
    public void MonoSetsNoColourAtAll()
    {
        Assert.All(Roles(Theme.Mono), role =>
        {
            var style = Style.Parse(role);
            Assert.Equal(Color.Default, style.Foreground);
            Assert.Equal(Color.Default, style.Background);
        });
    }

    [Theory]
    [InlineData("Critical", "red")]
    [InlineData("Warning", "yellow")]
    [InlineData("Info", "blue")]
    [InlineData("Good", "green")]
    public void DefaultKeepsTheFamiliarHealthColours(string level, string expected) =>
        Assert.Equal(expected, Theme.Default.Of(Enum.Parse<HealthLevel>(level)));
}

public class ThemeResolverTests
{
    [Theory]
    [InlineData(null, true, true, "default", false)]
    [InlineData(null, false, true, "mono", false)]
    [InlineData("default", true, true, "default", false)]
    [InlineData("high-contrast", false, true, "high-contrast", true)]
    [InlineData("high-contrast", false, false, "mono", false)]
    [InlineData("mono", true, true, "mono", false)]
    public void PicksTheThemeAndWhetherToForceColour(
        string? requested, bool consoleHasColor, bool canForceColor, string expected, bool forceColor)
    {
        var (theme, force) = ThemeResolver.Resolve(
            requested is null ? null : Theme.Find(requested), consoleHasColor, canForceColor);

        Assert.Equal(expected, theme.Name);
        Assert.Equal(forceColor, force);
    }
}

public class HealthStyleTests
{
    [Theory]
    [InlineData("Nominal", "Good")]
    [InlineData("Fair", "Good")]
    [InlineData("Serious", "Warning")]
    [InlineData("Critical", "Critical")]
    public void MapsThermalStates(string state, string level) =>
        Assert.Equal(Enum.Parse<HealthLevel>(level), HealthStyle.LevelOf(Enum.Parse<ThermalState>(state)));

    [Theory]
    [InlineData("Normal", "Good")]
    [InlineData("Warning", "Warning")]
    [InlineData("Critical", "Critical")]
    public void MapsMemoryPressure(string pressure, string level) =>
        Assert.Equal(Enum.Parse<HealthLevel>(level), HealthStyle.LevelOf(Enum.Parse<MemoryPressure>(pressure)));

    [Fact]
    public void PadsTokensToFourColumns()
    {
        Assert.Equal("OK  ", HealthStyle.PaddedToken(HealthLevel.Good));
        Assert.Equal("CRIT", HealthStyle.PaddedToken(HealthLevel.Critical));
    }
}
