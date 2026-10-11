using System.Text;
using Vimon.Core;

namespace Vimon.Cli;

/// <summary>
/// One- or two-line view for small terminals, tmux and status bars:
/// <c>WARN │ CPU 47°C 33% │ GPU 41°C 18% │ RAM 81%</c>, plus a line with the findings when
/// something is not healthy. Missing values are left out instead of shown as dashes. Pure.
/// </summary>
internal static class CompactRenderer
{
    private const string Separator = " │ ";
    private const string FindingSeparator = " · ";
    private const string FindingIndent = "  ";

    /// <param name="maxWidth">
    /// Live view: every line is cut or padded to exactly this width, so a shorter frame overwrites
    /// the previous one completely. Null (one-shot / status bars): lines are left as they are.
    /// </param>
    public static string Render(
        SystemSnapshot s,
        GpuMetrics? gpu,
        IReadOnlyList<Diagnostic> diagnostics,
        int selectedGpu,
        int? maxWidth)
    {
        var findings = diagnostics.Where(d => d.Level != HealthLevel.Good).ToList();
        var worst = diagnostics.Count == 0 ? HealthLevel.Good : diagnostics.Max(d => d.Level);

        var segments = new List<string> { LevelToken(worst) };
        AddSegment(segments, "CPU", Temperature(s.Cpu?.TempC), Percent(s.Cpu?.LoadPct));

        if (gpu is not null)
        {
            string label = s.Gpus.Count > 1 ? $"GPU {selectedGpu + 1}/{s.Gpus.Count}" : "GPU";
            AddSegment(segments, label, Temperature(gpu.CoreTempC), Percent(gpu.UtilizationPct));
        }

        AddSegment(segments, "RAM", Percent(s.Memory.LoadPct));

        var lines = new List<string> { string.Join(Separator, segments) };

        if (findings.Count > 0)
            lines.Add(FindingIndent + string.Join(FindingSeparator, findings.Select(f => f.Message)));

        var b = new StringBuilder();
        foreach (string line in lines)
            b.AppendLine(maxWidth is int width ? Fit(line, width) : line);

        return b.ToString();
    }

    private static void AddSegment(List<string> segments, string label, params string?[] values)
    {
        var present = values.OfType<string>().ToList();

        if (present.Count > 0)
            segments.Add($"{label} {string.Join(' ', present)}");
    }

    private static string LevelToken(HealthLevel level) => level switch
    {
        HealthLevel.Critical => "CRIT",
        HealthLevel.Warning => "WARN",
        HealthLevel.Info => "INFO",
        _ => "OK"
    };

    private static string? Temperature(double? value) =>
        IsPresent(value) && value > 0 ? $"{value:0}°C" : null;

    private static string? Percent(double? value) =>
        IsPresent(value) ? $"{value:0}%" : null;

    private static bool IsPresent(double? value) =>
        value is double v && !double.IsNaN(v) && !double.IsInfinity(v);

    private static string Fit(string line, int width)
    {
        if (width <= 0)
            return line;

        return line.Length > width
            ? line[..(width - 1)] + "…"
            : line.PadRight(width);
    }
}
