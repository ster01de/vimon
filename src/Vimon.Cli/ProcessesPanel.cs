using Spectre.Console;
using Spectre.Console.Rendering;
using Vimon.Core;

namespace Vimon.Cli;

/// <summary>The PROCESSES panel body: name, CPU share and memory of the busiest processes. Pure.</summary>
internal static class ProcessesPanel
{
    public const int NameWidth = 20;

    /// <returns>The panel title, or null when the backend reads no processes.</returns>
    public static string? Title(SystemSnapshot s) => s.ProcessScope switch
    {
        ProcessScope.None => null,
        ProcessScope.CurrentUser => "PROCESSES · yours",
        _ => "PROCESSES"
    };

    /// <returns>Null when hiding missing values and there is no process to show.</returns>
    public static IRenderable? Body(SystemSnapshot s, bool hideMissing, Theme theme)
    {
        if (s.TopProcesses.Count == 0)
            return hideMissing ? null : new Markup($"[{theme.Muted}]{Dashboard.Missing}[/]");

        var grid = new Grid();
        grid.AddColumn(new GridColumn { Width = NameWidth, NoWrap = true });
        grid.AddColumn(new GridColumn { Alignment = Justify.Right, NoWrap = true });
        grid.AddColumn(new GridColumn { Alignment = Justify.Right, NoWrap = true });

        foreach (var process in s.TopProcesses)
        {
            // Cut before escaping, so an escaped '[' can never be split in half.
            string name = Markup.Escape(MetricFormat.Truncate(process.Name, NameWidth));
            string cpu = process.CpuPct is double pct ? $"{pct:0.0} %" : $"[{theme.Muted}]{Dashboard.Missing}[/]";

            grid.AddRow(new Markup(name), new Markup(cpu), new Markup(MetricFormat.Memory(process.ResidentBytes)));
        }

        return grid;
    }
}
