namespace Vimon.Core;

public static class DiskTotals
{
    /// <returns>The sum of the known rates of all drives; null per direction when no drive reports one.</returns>
    public static (double? ReadBytesPerSec, double? WriteBytesPerSec) Of(IReadOnlyList<StorageMetrics> drives) =>
        (KnownRates.Sum(drives.Select(d => d.ReadBytesPerSec)), KnownRates.Sum(drives.Select(d => d.WriteBytesPerSec)));
}

internal static class KnownRates
{
    /// <returns>The sum of the values that are known; null when none is.</returns>
    public static double? Sum(IEnumerable<double?> values)
    {
        var known = values.OfType<double>().ToList();
        return known.Count == 0 ? null : known.Sum();
    }
}
