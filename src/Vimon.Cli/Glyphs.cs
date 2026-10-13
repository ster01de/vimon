namespace Vimon.Cli;

/// <summary>
/// The symbols vimon draws itself. The classic Windows console (conhost) uses fonts such as
/// Consolas that lack block eighths and some symbols, and shows them as '?'. There the safe
/// set uses plain ASCII; Spectre swaps its spinner and rounded borders on its own once Unicode is
/// switched off.
/// </summary>
internal sealed record Glyphs(string SparkBlocks, string Bullet, char BarFull, char BarEmpty)
{
    public static readonly Glyphs Unicode = new("▁▂▃▄▅▆▇█", "●", '█', '░');

    public static readonly Glyphs Safe = new("_.-:=+*#", "*", '#', '.');

    /// <summary>Maps a sparkline drawn with <see cref="Unicode"/> blocks onto this set.</summary>
    public string Spark(string unicodeBlocks)
    {
        if (ReferenceEquals(this, Unicode))
            return unicodeBlocks;

        return new string(unicodeBlocks
            .Select(c => Unicode.SparkBlocks.IndexOf(c) is int level and >= 0 ? SparkBlocks[level] : c)
            .ToArray());
    }

    /// <summary>
    /// The classic console host on Windows: neither Windows Terminal (sets WT_SESSION) nor an editor
    /// terminal such as VS Code (sets TERM_PROGRAM).
    /// </summary>
    public static bool IsLegacyWindowsConsole(bool isWindows, string? wtSession, string? termProgram) =>
        isWindows && string.IsNullOrEmpty(wtSession) && string.IsNullOrEmpty(termProgram);
}
