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
    /// <returns>The theme, and whether colour has to be switched back on for it.</returns>
    public static (Theme Theme, bool ForceColor) Resolve(Theme? requested, bool consoleHasColor)
    {
        if (requested is null)
            return (consoleHasColor ? Theme.Default : Theme.Mono, ForceColor: false);

        return (requested, ForceColor: requested.UsesColor && !consoleHasColor);
    }
}
