namespace Vimon.Cli;

internal sealed record FittedFrame(string Text, int LineCount);

/// <summary>Fits a rendered frame into the visible console window. Pure: no console access.</summary>
internal static class TerminalFrame
{
    /// <summary>
    /// Truncates frames taller than the window, keeping one row free so the final newline cannot
    /// scroll the screen, and blanks out rows left over from a taller previous frame.
    /// </summary>
    /// <param name="windowHeight">Visible rows; 0 or less means unknown, and the frame is not truncated.</param>
    public static FittedFrame Fit(string frame, int windowHeight, int previousLineCount)
    {
        var lines = frame
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .ToList();

        if (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        int width = lines.Count == 0 ? 0 : lines.Max(line => line.Length);
        bool isHeightKnown = windowHeight > 0;
        int maxLines = Math.Max(1, windowHeight - 1);

        if (isHeightKnown && lines.Count > maxLines)
        {
            int keep = maxLines - 1;
            int hidden = lines.Count - keep;
            lines = lines.Take(keep).Append(Hint(hidden, width)).ToList();
        }

        int blankLines = Math.Max(0, previousLineCount - lines.Count);
        var output = lines.Concat(Enumerable.Repeat(new string(' ', width), blankLines));

        return new FittedFrame(
            string.Concat(output.Select(line => line + Environment.NewLine)),
            lines.Count);
    }

    private static string Hint(int hiddenLines, int width)
    {
        string hint = $"… {hiddenLines} more lines, enlarge the terminal";
        return width > 0 && hint.Length > width ? hint[..width] : hint.PadRight(width);
    }
}
