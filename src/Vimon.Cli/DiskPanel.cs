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
    private const int BarWidth = 10;
    private const int NameWidth = 18;

    /// <returns>Label/value rows; a null value is a missing reading.</returns>
    public static List<(string Label, string? Value)> Rows(SystemSnapshot s, HealthThresholds t, Theme theme)
    {
        // The startup disk first, then the others by name, so rows keep their place.
        var rows = s.Volumes
            .OrderBy(v => v.Name != VolumeFilter.StartupDiskName)
            .ThenBy(v => v.Name, StringComparer.Ordinal)
            .Take(MaxVolumes)
            .Select(v => (MetricFormat.Truncate(v.Name, NameWidth), (string?)Fill(v, t, theme)))
            .ToList();

        rows.AddRange(s.Storage
            .Take(MaxDrives)
            .Select(drive => (MetricFormat.Truncate(drive.Name, NameWidth), Temperature(drive.TempC))));

        return rows;
    }

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
