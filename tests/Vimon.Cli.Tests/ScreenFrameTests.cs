using System.Text.RegularExpressions;
using Spectre.Console;

namespace Vimon.Cli.Tests;

public class ScreenFrameTests
{
    private const string Escape = "\u001b";

    private static string Frame(int lines) => string.Join("\n", Enumerable.Range(1, lines).Select(i => $"line {i}")) + "\n";

    private static string[] VisibleLines(string composed) =>
        Regex.Replace(composed, @"\u001b\[[0-9;]*[A-Za-z]", "").Split("\r\n");

    [Fact]
    public void StartsAtTheTopLeftAndClearsEverythingBelow()
    {
        string composed = ScreenFrame.Compose(Frame(3), height: 10, clearScreen: false);

        Assert.StartsWith($"{Escape}[H", composed);
        Assert.EndsWith($"{Escape}[K{Escape}[J", composed);
    }

    [Fact]
    public void ClearsTheRestOfEveryLineSoShorterFramesLeaveNoTraces()
    {
        string composed = ScreenFrame.Compose(Frame(3), height: 10, clearScreen: false);

        Assert.Equal(3, Regex.Matches(composed, @"\u001b\[K").Count);
        Assert.Equal(new[] { "line 1", "line 2", "line 3" }, VisibleLines(composed));
    }

    [Fact]
    public void CutsTheFrameToTheWindowWithoutEverScrolling()
    {
        string composed = ScreenFrame.Compose(Frame(50), height: 10, clearScreen: false);

        var lines = VisibleLines(composed);
        Assert.Equal(10, lines.Length);
        Assert.Equal("line 1", lines[0]);
        // The marker sits on the bottom row and no line break follows it.
        Assert.EndsWith($"…{Escape}[K{Escape}[J", composed);
    }

    [Fact]
    public void KeepsEveryLineWhenTheHeightIsUnknown()
    {
        Assert.Equal(50, VisibleLines(ScreenFrame.Compose(Frame(50), height: 0, clearScreen: false)).Length);
    }

    [Fact]
    public void WipesTheWholeScreenOnlyWhenAsked()
    {
        Assert.DoesNotContain($"{Escape}[2J", ScreenFrame.Compose(Frame(3), 10, clearScreen: false));
        Assert.StartsWith($"{Escape}[2J{Escape}[H", ScreenFrame.Compose(Frame(3), 10, clearScreen: true));
    }

    [Fact]
    public void AcceptsWindowsLineEndings()
    {
        Assert.Equal(new[] { "a", "b" }, VisibleLines(ScreenFrame.Compose("a\r\nb\r\n", 10, clearScreen: false)));
    }

    [Fact]
    public void RendersAViewNoWiderThanTheWindow()
    {
        var panel = new Panel(new Text(new string('x', 200))) { Expand = true };

        string rendered = ScreenFrame.Render(panel, width: 40, ColorSystem.Standard, unicode: true);

        var lines = Regex.Replace(rendered, @"\u001b\[[0-9;]*m", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.All(lines, line => Assert.True(line.TrimEnd('\r').Length <= 40, $"'{line}' is wider than 40"));
    }

    [Fact]
    public void KeepsColoursInTheRenderedText()
    {
        string rendered = ScreenFrame.Render(new Markup("[red]hot[/]"), 40, ColorSystem.Standard, unicode: true);

        Assert.Contains($"{Escape}[", rendered);
        Assert.Contains("hot", rendered);
    }
}
