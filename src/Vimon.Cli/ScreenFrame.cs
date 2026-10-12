using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Vimon.Cli;

/// <summary>
/// Draws the live view like <c>top</c>: every frame starts at the top-left corner, clears the rest
/// of each line and everything below, and never reaches past the last row. Positions are absolute,
/// so a resized window is repaired by the next frame. Spectre's Live display moves the cursor
/// relative to the previous frame instead, which breaks once the terminal re-wraps lines or the
/// frame fills the window and scrolls it.
/// </summary>
internal static class ScreenFrame
{
    private const string Home = "\u001b[H";
    private const string ClearLineEnd = "\u001b[K";
    private const string ClearBelow = "\u001b[J";
    private const string ClearScreen = "\u001b[2J";
    private const string LineBreak = "\r\n";
    private const string OverflowMarker = "…";

    /// <summary>Renders <paramref name="view"/> to text with ANSI styles, at most <paramref name="width"/> columns wide.</summary>
    public static string Render(IRenderable view, int width, ColorSystem colorSystem, bool unicode)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = (ColorSystemSupport)colorSystem,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = width;
        console.Profile.Capabilities.Unicode = unicode;
        console.Write(view);
        return writer.ToString();
    }

    /// <param name="height">Window rows; 0 or less when unknown, then nothing is cut.</param>
    /// <param name="clearScreen">Wipe the whole screen first, for a redraw the user asked for.</param>
    /// <returns>One write that paints the frame; cut lines end in an overflow marker.</returns>
    public static string Compose(string rendered, int height, bool clearScreen)
    {
        var lines = rendered.Replace(LineBreak, "\n").Split('\n').ToList();

        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        // Bottom rows go first, so STATUS stays visible in a short window.
        if (height > 0 && lines.Count > height)
            lines = [.. lines.Take(height - 1), OverflowMarker];

        var frame = new StringBuilder();

        if (clearScreen)
            frame.Append(ClearScreen);

        // No line break after the last line: a break on the bottom row would scroll the screen.
        return frame
            .Append(Home)
            .Append(string.Join(ClearLineEnd + LineBreak, lines))
            .Append(ClearLineEnd)
            .Append(ClearBelow)
            .ToString();
    }
}
