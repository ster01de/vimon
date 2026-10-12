namespace Vimon.Core;

public static class NetworkTotals
{
    /// <returns>The sum of the known rates per direction; null when no interface reports one.</returns>
    public static (double? DownBytesPerSec, double? UpBytesPerSec) Of(IReadOnlyList<InterfaceRate> rates) =>
        (KnownRates.Sum(rates.Select(r => r.DownBytesPerSec)), KnownRates.Sum(rates.Select(r => r.UpBytesPerSec)));
}
