namespace Vimon.Core;

public static class NetworkTotals
{
    /// <returns>The sum of the known rates per direction; null when no interface reports one.</returns>
    public static (double? DownBytesPerSec, double? UpBytesPerSec) Of(IReadOnlyList<InterfaceRate> rates) =>
        (Sum(rates.Select(r => r.DownBytesPerSec)), Sum(rates.Select(r => r.UpBytesPerSec)));

    private static double? Sum(IEnumerable<double?> values)
    {
        var known = values.OfType<double>().ToList();
        return known.Count == 0 ? null : known.Sum();
    }
}
