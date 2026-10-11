namespace Vimon.Cli;

internal enum ViewMode { Full, Compact, Widget }

/// <summary>Command-line flags. <c>--widget</c> wins over <c>--compact</c>.</summary>
internal sealed record CliOptions(bool Once, ViewMode Mode, bool HideMissing)
{
    private const string OnceFlag = "--once";
    private const string CompactFlag = "--compact";
    private const string WidgetFlag = "--widget";
    private const string HideMissingFlag = "--hide-missing";

    private static readonly string[] KnownFlags = [OnceFlag, CompactFlag, WidgetFlag, HideMissingFlag];

    /// <exception cref="ArgumentException">An argument is not a known flag.</exception>
    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        string? unknown = args.FirstOrDefault(arg => !KnownFlags.Contains(arg));

        if (unknown is not null)
            throw new ArgumentException($"unknown option '{unknown}'. Options: {string.Join(", ", KnownFlags)}");

        var mode = args.Contains(WidgetFlag) ? ViewMode.Widget
            : args.Contains(CompactFlag) ? ViewMode.Compact
            : ViewMode.Full;

        return new CliOptions(args.Contains(OnceFlag), mode, args.Contains(HideMissingFlag));
    }

    /// <summary>The C key switches full and compact; the widget stays a widget.</summary>
    public static ViewMode ToggleCompact(ViewMode mode) => mode switch
    {
        ViewMode.Full => ViewMode.Compact,
        ViewMode.Compact => ViewMode.Full,
        _ => mode
    };
}
