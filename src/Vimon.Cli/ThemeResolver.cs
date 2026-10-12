namespace Vimon.Cli;

/// <summary>
/// Picks the theme. Spectre.Console already turns colour off for NO_COLOR (also when it is empty),
/// TERM=dumb and redirected output, so its detection stands for all of them. A theme chosen with
/// <c>--theme</c> wins over NO_COLOR, as no-color.org asks of command-line flags.
/// </summary>
internal static class ThemeResolver
{
    /// <param name="requested">The validated <c>--theme</c> value, or null when none was given.</param>
    /// <param name="consoleHasColor">Whether Spectre detected colour support.</param>
    /// <param name="canForceColor">Whether the terminal understands ANSI escape codes at all.</param>
    /// <returns>The theme, and whether colour has to be switched back on for it.</returns>
    public static (Theme Theme, bool ForceColor) Resolve(Theme? requested, bool consoleHasColor, bool canForceColor)
    {
        if (requested is null || !requested.UsesColor || consoleHasColor)
            return (requested ?? (consoleHasColor ? Theme.Default : Theme.Mono), ForceColor: false);

        // A colour theme on a terminal that cannot show colour would lose the text markers as well.
        return canForceColor ? (requested, ForceColor: true) : (Theme.Mono, ForceColor: false);
    }
}
