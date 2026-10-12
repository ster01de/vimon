using Spectre.Console;
using Vimon.Core;

namespace Vimon.Cli;

/// <summary>
/// Rows of the DISK panel: how full each volume is (coloured with the same limits as the status
/// check) and drive temperatures where the platform reports them. Pure.
/// </summary>
internal static class DiskPanel
{
    public const int MaxVolumes = 3;

    private const int MaxDrives = 3;
    // Sized so a row fits a side-by-side panel at 100 columns without wrapping.
    private const int BarWidth = 6;
    private const int NameWidth = 16;
    private const int RateWidth = 10;

    /// <returns>Label/value rows; a null value is a missing reading.</returns>
    public static List<(string Label, string? Value)> Rows(SystemSnapshot s, MetricHistory h, HealthThresholds t, Theme theme)
    {
        var rows = new List<(string Label, string? Value)>();
        var (read, write) = DiskTotals.Of(s.Storage);

        if (read is not null || write is not null)
        {
            rows.Add(("Read", Rate(read, h.DiskReadBps, theme)));
            rows.Add(("Write", Rate(write, h.DiskWriteBps, theme)));
        }

        // The startup disk first, then the others by name, so rows keep their place.
        rows.AddRange(s.Volumes
            .OrderBy(v => v.Name != VolumeFilter.StartupDiskName)
            .ThenBy(v => v.Name, StringComparer.Ordinal)
            .Take(MaxVolumes)
            .Select(v => (MetricFormat.Truncate(v.Name, NameWidth), (string?)Fill(v, t, theme))));

        // Busiest first; the name breaks ties so rows do not swap places while drives are idle.
        rows.AddRange(s.Storage
            .OrderByDescending(d => (d.ReadBytesPerSec ?? 0) + (d.WriteBytesPerSec ?? 0))
            .ThenBy(d => d.Name, StringComparer.Ordinal)
            .Take(MaxDrives)
            .SelectMany(drive => DriveRows(drive, theme)));

        return rows;
    }

    /// <summary>The drive row, plus a row with its USB link below, so neither gets too long for a side-by-side panel.</summary>
    private static IEnumerable<(string Label, string? Value)> DriveRows(StorageMetrics drive, Theme theme)
    {
        yield return (MetricFormat.Truncate(drive.Name, NameWidth), Drive(drive, theme));

        if (drive.UsbLink is UsbSpeed link)
        {
            string color = UsbSpeeds.IsSlow(link) ? theme.Info : theme.Muted;
            yield return ("", $"[{color}]↳ {UsbSpeeds.Describe(link)}[/]");
        }
    }

    private static string? Rate(double? bytesPerSec, IReadOnlyList<double> history, Theme theme)
    {
        if (MetricFormat.Throughput(bytesPerSec) is not string text)
            return null;

        string blocks = Sparkline.ForThroughput(history);
        return blocks.Length == 0 ? text : $"{text,-RateWidth} [{theme.Accent}]{blocks}[/]";
    }

    /// <returns>Rates, attachment and temperature of one drive, or null when none is known.</returns>
    private static string? Drive(StorageMetrics drive, Theme theme)
    {
        var parts = new List<string>();

        if (drive.ReadBytesPerSec is not null || drive.WriteBytesPerSec is not null)
        {
            parts.Add($"R {MetricFormat.Throughput(drive.ReadBytesPerSec) ?? Dashboard.Missing}  " +
                      $"W {MetricFormat.Throughput(drive.WriteBytesPerSec) ?? Dashboard.Missing}");
        }

        if (Attachment(drive) is string attachment)
            parts.Add($"[{theme.Muted}]{Markup.Escape(attachment)}[/]");

        if (Temperature(drive.TempC) is string temperature)
            parts.Add(temperature);

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>"USB external" says what matters; internal buses ("Apple Fabric", "PCI-Express") just read "internal".</summary>
    private static string? Attachment(StorageMetrics drive) => (drive.Interconnect, drive.IsExternal) switch
    {
        (_, false) => "internal",
        (string link, true) => $"{link} external",
        (null, true) => "external",
        (string link, null) => link,
        _ => null
    };

    private static string Fill(VolumeSpace volume, HealthThresholds t, Theme theme)
    {
        string color = theme.Of(DiagnosticsEngine.VolumeLevel(volume, t));
        int filled = MetricFormat.FilledCells(volume.UsedPct, BarWidth);
        string bar = $"[{color}]{new string('█', filled)}[/][{theme.Muted}]{new string('░', BarWidth - filled)}[/]";
        string fileSystem = volume.FileSystem.Length > 0 ? $" [{theme.Muted}]{Markup.Escape(volume.FileSystem)}[/]" : "";

        return $"{bar} [{color}]{volume.UsedPct:0} %[/] · {MetricFormat.Memory(volume.FreeBytes)} free{fileSystem}";
    }

    private static string? Temperature(double? tempC) =>
        MetricFormat.IsPresent(tempC) && tempC > 0 ? $"{tempC:0.#} °C" : null;
}
