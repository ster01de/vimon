using System.Text;
using Vimon.Core;

namespace Vimon.Cli;

/// <summary>
/// One- or two-line view for small terminals, tmux and status bars:
/// <c>WARN │ CPU 47°C 33% │ GPU 41°C 18% │ RAM 81% │ NET ↓1.2M ↑310K</c>, plus a line with the findings when
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
        var segments = new List<string> { HealthStyle.Token(HealthStyle.Worst(diagnostics)) };
        AddSegment(segments, "CPU", MetricFormat.ShortTemperature(s.Cpu?.TempC), MetricFormat.ShortPercent(s.Cpu?.LoadPct));

        if (gpu is not null)
        {
            string label = s.Gpus.Count > 1 ? $"GPU {selectedGpu + 1}/{s.Gpus.Count}" : "GPU";
            AddSegment(segments, label, MetricFormat.ShortTemperature(gpu.CoreTempC), MetricFormat.ShortPercent(gpu.UtilizationPct));
        }

        AddSegment(segments, "RAM", MetricFormat.ShortPercent(s.Memory.LoadPct));

        var (down, up) = NetworkTotals.Of(s.Network);
        AddSegment(segments, "NET", Arrow("↓", down), Arrow("↑", up));

        var lines = new List<string> { string.Join(Separator, segments) };

        if (findings.Count > 0)
            lines.Add(FindingIndent + string.Join(FindingSeparator, findings.Select(f => f.Message)));

        var b = new StringBuilder();
        foreach (string line in lines)
            b.AppendLine(maxWidth is int width ? MetricFormat.Fit(line, width) : line);

        return b.ToString();
    }

    private static string? Arrow(string arrow, double? bytesPerSec) =>
        MetricFormat.ShortThroughput(bytesPerSec) is string text ? arrow + text : null;

    private static void AddSegment(List<string> segments, string label, params string?[] values)
    {
        var present = values.OfType<string>().ToList();

        if (present.Count > 0)
            segments.Add($"{label} {string.Join(' ', present)}");
    }
}
