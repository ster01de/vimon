namespace Vimon.Cli;

/// <summary>
/// Alternate screen buffer, as used by top, htop and less: the live view gets its own screen without
/// scrollback, and the user's previous terminal content comes back on exit. Without it, every
/// clear on resize pushes a full frame into the macOS Terminal scrollback.
/// </summary>
internal static class TerminalScreen
{
    public const string EnterAlternateScreen = "\u001b[?1049h";
    public const string LeaveAlternateScreen = "\u001b[?1049l";

    /// <remarks>
    /// Skipped on Windows, where legacy consoles only honour VT sequences after an explicit opt-in
    /// and Console.Clear does not fill the scrollback anyway, and when output is redirected.
    /// </remarks>
    public static bool ShouldUseAlternateScreen(bool isWindows, bool isOutputRedirected) =>
        !isWindows && !isOutputRedirected;
}
