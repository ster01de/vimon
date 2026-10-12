using Spectre.Console;
using Vimon.Core;

namespace Vimon.Cli;

/// <summary>
/// Spectre markup styles for every role in the views, so colours live in one place.
/// </summary>
/// <param name="Muted">Labels, missing values, empty bar cells, key help and borders.</param>
/// <param name="Accent">Sparklines and the GPU memory bar.</param>
/// <param name="UsesColor">False for a theme that only uses text and bold.</param>
/// <param name="MarksFindings">Prefix status findings with OK/INFO/WARN/CRIT instead of a coloured dot.</param>
internal sealed record Theme(
    string Name,
    string Good,
    string Info,
    string Warning,
    string Critical,
    string Muted,
    string Accent,
    bool UsesColor,
    bool MarksFindings)
{
    public static readonly Theme Default = new(
        "default", "green", "blue", "yellow", "red", "grey", "steelblue", UsesColor: true, MarksFindings: false);

    // Status stays readable without colour: text tokens, bold for warnings, distinct bar glyphs.
    public static readonly Theme Mono = new(
        "mono", "default", "default", "bold", "bold", "default", "default", UsesColor: false, MarksFindings: true);

    // Bright palette colours for dark or transparent backgrounds such as the kitty desktop widget.
    public static readonly Theme HighContrast = new(
        "high-contrast", "lime", "aqua", "yellow", "bold red", "silver", "white", UsesColor: true, MarksFindings: false);

    public static readonly IReadOnlyList<Theme> All = [Default, Mono, HighContrast];

    public static string NameList => string.Join(", ", All.Select(theme => theme.Name));

    /// <returns>The theme with this name, ignoring case; null when there is none.</returns>
    public static Theme? Find(string name) =>
        All.FirstOrDefault(theme => string.Equals(theme.Name, name, StringComparison.OrdinalIgnoreCase));

    public string Of(HealthLevel level) => level switch
    {
        HealthLevel.Critical => Critical,
        HealthLevel.Warning => Warning,
        HealthLevel.Info => Info,
        _ => Good
    };

    public Style StyleOf(HealthLevel level) => Style.Parse(Of(level));

    public Style MutedStyle => Style.Parse(Muted);

    /// <summary>The symbols to draw with; <see cref="Glyphs.Safe"/> in consoles whose font lacks them.</summary>
    public Glyphs Glyphs { get; init; } = Glyphs.Unicode;
}
