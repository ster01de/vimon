using Spectre.Console;
using Spectre.Console.Rendering;
using Vimon.Core;

namespace Vimon.Cli;

/// <summary>The PROCESSES panel body: name, CPU share and memory of the busiest processes. Pure.</summary>
internal static class ProcessesPanel
{
    public const int NameWidth = 20;
    /// <summary>As many as the backends read (MacProcesses.TopCount).</summary>
    public const int MaxRows = 5;

    // "100.0 %" and "123.4 GB", so the columns stay put while values change.
    private const int CpuWidth = 7;
    private const int MemoryWidth = 8;

    /// <returns>The panel title, or null when the backend reads no processes.</returns>
    public static string? Title(SystemSnapshot s) => s.ProcessScope switch
    {
        ProcessScope.None => null,
        ProcessScope.CurrentUser => "PROCESSES · yours",
        _ => "PROCESSES"
    };

    /// <returns>Null when hiding missing values and there is no process to show.</returns>
    /// <remarks>Always <see cref="MaxRows"/> lines, so the panel does not grow once processes are sampled.</remarks>
    public static IRenderable? Body(SystemSnapshot s, bool hideMissing, Theme theme)
    {
        if (s.TopProcesses.Count == 0 && hideMissing)
            return null;

        var grid = new Grid();
        grid.AddColumn(new GridColumn { Width = NameWidth, NoWrap = true });
        grid.AddColumn(new GridColumn { Width = CpuWidth, Alignment = Justify.Right, NoWrap = true });
        grid.AddColumn(new GridColumn { Width = MemoryWidth, Alignment = Justify.Right, NoWrap = true });

        var shown = s.TopProcesses.Take(MaxRows).ToList();

        if (shown.Count == 0)
            grid.AddRow(new Markup($"[{theme.Muted}]{Dashboard.Missing}[/]"), new Text(""), new Text(""));

        foreach (var process in shown)
        {
            // Cut before escaping, so an escaped '[' can never be split in half.
            string name = Markup.Escape(MetricFormat.Truncate(process.Name, NameWidth));
            string cpu = process.CpuPct is double pct ? $"{pct:0.0} %" : $"[{theme.Muted}]{Dashboard.Missing}[/]";

            grid.AddRow(new Markup(name), new Markup(cpu), new Markup(MetricFormat.Memory(process.ResidentBytes)));
        }

        for (int row = Math.Max(shown.Count, 1); row < MaxRows; row++)
            grid.AddRow(new Text(" "), new Text(""), new Text(""));

        return grid;
    }
}
