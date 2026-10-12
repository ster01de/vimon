namespace Vimon.Cli;

internal enum ViewMode { Full, Compact, Widget }

/// <summary>Command-line flags. <c>--widget</c> wins over <c>--compact</c>.</summary>
/// <param name="Theme">The theme from <c>--theme</c>; null when none was given.</param>
/// <param name="NoProcesses">Hide process names, e.g. while sharing the screen.</param>
internal sealed record CliOptions(bool Once, ViewMode Mode, bool HideMissing, Theme? Theme = null, bool NoProcesses = false)
{
    private const string OnceFlag = "--once";
    private const string CompactFlag = "--compact";
    private const string WidgetFlag = "--widget";
    private const string HideMissingFlag = "--hide-missing";
    private const string NoProcessesFlag = "--no-processes";
    private const string ThemeFlag = "--theme";
    private const string ThemeAssignment = ThemeFlag + "=";
    private const string OptionPrefix = "--";

    private static readonly string[] Switches = [OnceFlag, CompactFlag, WidgetFlag, HideMissingFlag, NoProcessesFlag];

    private static string Usage => $"Options: {string.Join(", ", Switches)}, {ThemeFlag} <name>. Themes: {Cli.Theme.NameList}";

    /// <exception cref="ArgumentException">An argument is unknown, or a theme is missing or unknown.</exception>
    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        var switches = new HashSet<string>(StringComparer.Ordinal);
        Theme? theme = null;

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];

            if (Switches.Contains(arg))
                switches.Add(arg);
            else if (arg == ThemeFlag)
                theme = ReadTheme(i + 1 < args.Count ? args[++i] : null);
            else if (arg.StartsWith(ThemeAssignment, StringComparison.Ordinal))
                theme = ReadTheme(arg[ThemeAssignment.Length..]);
            else
                throw new ArgumentException($"unknown option '{arg}'. {Usage}");
        }

        var mode = switches.Contains(WidgetFlag) ? ViewMode.Widget
            : switches.Contains(CompactFlag) ? ViewMode.Compact
            : ViewMode.Full;

        return new CliOptions(
            switches.Contains(OnceFlag), mode, switches.Contains(HideMissingFlag), theme, switches.Contains(NoProcessesFlag));
    }

    /// <summary>The C key switches full and compact; the widget stays a widget.</summary>
    public static ViewMode ToggleCompact(ViewMode mode) => mode switch
    {
        ViewMode.Full => ViewMode.Compact,
        ViewMode.Compact => ViewMode.Full,
        _ => mode
    };

    private static Theme ReadTheme(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.StartsWith(OptionPrefix, StringComparison.Ordinal))
            throw new ArgumentException($"option '{ThemeFlag}' needs a value. Themes: {Cli.Theme.NameList}");

        return Cli.Theme.Find(name)
            ?? throw new ArgumentException($"unknown theme '{name}'. Themes: {Cli.Theme.NameList}");
    }
}
