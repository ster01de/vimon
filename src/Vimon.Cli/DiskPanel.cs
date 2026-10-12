using Spectre.Console;
using Vimon.Core;

namespace Vimon.Cli;

/// <summary>
/// Rows of the DISK panel: total throughput, the process causing most of it, how full each volume is
/// (coloured with the same limits as the status check) and one row per drive. Rows keep their place:
/// they exist as soon as there is a drive and every value sits in a column of fixed width. Pure.
/// </summary>
internal static class DiskPanel
{
    public const int MaxVolumes = 3;

    private const int MaxDrives = 3;
    // Sized so a row fits a side-by-side panel at 100 columns (45 cells after the 10-cell label) without wrapping.
    private const int BarWidth = 6;
    private const int PercentWidth = 5;
    private const int FreeWidth = 8;
    private const int RateWidth = MetricFormat.ThroughputWidth;
    // "R 999 KB/s W 999 KB/s"
    private const int RateCellsWidth = 2 * (2 + RateWidth) + 1;
    // "USB 480 Mb/s"
    private const int TailWidth = 12;
    private const int ProcessNameWidth = 12;
    private const string StartupLabel = "Startup";
    private const string Idle = "idle";

    /// <returns>Label/value rows; a null value is a missing reading.</returns>
    public static List<(string Label, string? Value)> Rows(SystemSnapshot s, MetricHistory h, HealthThresholds t, Theme theme)
    {
        var rows = new List<(string Label, string? Value)>();
        var (read, write) = DiskTotals.Of(s.Storage);

        // Present from the first sample on, before there is a rate, so nothing below moves when one arrives.
        if (s.Storage.Count > 0)
        {
            rows.Add(("Read", Dashboard.Rate(read, h.DiskReadBps, theme)));
            rows.Add(("Write", Dashboard.Rate(write, h.DiskWriteBps, theme)));

            if (s.ProcessScope != ProcessScope.None)
                rows.Add(("Busiest", BusiestProcess(s.TopDiskProcesses, theme)));
        }

        // The startup disk first, then the others by name, so rows keep their place.
        rows.AddRange(s.Volumes
            .OrderBy(v => v.Name != VolumeFilter.StartupDiskName)
            .ThenBy(v => v.Name, StringComparer.Ordinal)
            .Take(MaxVolumes)
            .Select(v => (VolumeLabel(v), (string?)Fill(v, t, theme))));

        // By name, not by traffic, so drives do not swap places while a copy starts or ends.
        rows.AddRange(s.Storage
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .Take(MaxDrives)
            .Select(drive => (MetricFormat.Truncate(drive.Name, Dashboard.LabelWidth), Drive(drive, theme))));

        return rows;
    }

    /// <summary>
    /// Which process causes the traffic, e.g. <c>Finder       R    0 B/s W  110 MB/s</c>; a dash while
    /// nothing reads or writes, so the row stays.
    /// </summary>
    private static string BusiestProcess(IReadOnlyList<ProcessUsage> top, Theme theme)
    {
        var process = top.FirstOrDefault();

        if (process is null || (process.DiskReadBytesPerSec ?? 0) + (process.DiskWriteBytesPerSec ?? 0) <= 0)
            return $"[{theme.Muted}]{Dashboard.Missing}[/]";

        // Cut before escaping, so an escaped '[' can never be split in half.
        string name = Markup.Escape(MetricFormat.Truncate(process.Name, ProcessNameWidth).PadRight(ProcessNameWidth));
        return $"{name} {RateCells(process.DiskReadBytesPerSec, process.DiskWriteBytesPerSec, theme)}";
    }

    /// <returns>Rates and attachment of one drive in fixed columns, or null when neither is known.</returns>
    private static string? Drive(StorageMetrics drive, Theme theme)
    {
        string? tail = Tail(drive, theme);

        if (drive.ReadBytesPerSec is null && drive.WriteBytesPerSec is null && tail is null)
            return null;

        return tail is null
            ? RateCells(drive.ReadBytesPerSec, drive.WriteBytesPerSec, theme)
            : $"{RateCells(drive.ReadBytesPerSec, drive.WriteBytesPerSec, theme)} {tail}";
    }

    /// <summary><c>R  556 KB/s W 7.8 KB/s</c> in <see cref="RateCellsWidth"/> cells; a quiet <c>idle</c> without traffic.</summary>
    internal static string RateCells(double? read, double? write, Theme theme)
    {
        if (read is null && write is null)
            return $"[{theme.Muted}]{Dashboard.Missing.PadRight(RateCellsWidth)}[/]";

        if (read == 0 && write == 0)
            return $"[{theme.Muted}]{Idle.PadRight(RateCellsWidth)}[/]";

        return $"R {Cell(read)} W {Cell(write)}";
    }

    private static string Cell(double? bytesPerSec) =>
        MetricFormat.AlignRight(MetricFormat.Throughput(bytesPerSec) ?? Dashboard.Missing, RateWidth);

    /// <summary>
    /// How the drive is attached, in at most <see cref="TailWidth"/> cells: the USB link speed (the
    /// limit that matters for a copy), otherwise external/internal, or the temperature of an internal drive.
    /// </summary>
    private static string? Tail(StorageMetrics drive, Theme theme)
    {
        if (drive.UsbLink is UsbSpeed link)
        {
            string color = UsbSpeeds.IsSlow(link) ? theme.Info : theme.Muted;
            return $"[{color}]USB {UsbSpeeds.Rate(link)}[/]";
        }

        string? text = (drive.Interconnect, drive.IsExternal) switch
        {
            (_, false) => Temperature(drive.TempC) ?? "internal",
            (null, true) => "external",
            (string bus, _) => bus,
            _ => Temperature(drive.TempC)
        };

        return text is null ? null : $"[{theme.Muted}]{Markup.Escape(MetricFormat.Truncate(text, TailWidth))}[/]";
    }

    private static string VolumeLabel(VolumeSpace volume) =>
        volume.Name == VolumeFilter.StartupDiskName ? StartupLabel : MetricFormat.Truncate(volume.Name, Dashboard.LabelWidth);

    private static string Fill(VolumeSpace volume, HealthThresholds t, Theme theme)
    {
        string color = theme.Of(DiagnosticsEngine.VolumeLevel(volume, t));
        int filled = MetricFormat.FilledCells(volume.UsedPct, BarWidth);
        string bar = $"[{color}]{new string(theme.Glyphs.BarFull, filled)}[/][{theme.Muted}]{new string(theme.Glyphs.BarEmpty, BarWidth - filled)}[/]";
        string percent = MetricFormat.AlignRight($"{volume.UsedPct:0} %", PercentWidth);
        string free = MetricFormat.AlignRight(MetricFormat.Memory(volume.FreeBytes), FreeWidth);
        string fileSystem = volume.FileSystem.Length > 0 ? $" [{theme.Muted}]{Markup.Escape(volume.FileSystem)}[/]" : "";

        return $"{bar} [{color}]{percent}[/] {free} free{fileSystem}";
    }

    private static string? Temperature(double? tempC) =>
        MetricFormat.IsPresent(tempC) && tempC > 0 ? $"{tempC:0.#} °C" : null;
}
