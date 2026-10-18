using System.Collections.Immutable;
using Vimon.Core;

namespace Vimon.Cli;

/// <summary>
/// One displayed rate: an exponential moving average of the raw rates, so the number does not jump
/// with every sample. Traffic that stops shows as zero ("idle") within a second. Immutable.
/// </summary>
internal readonly record struct SmoothedRate(double Value, int ZeroRun)
{
    /// <summary>63 % of a step shows after one second, 90 % after about 2.3 s.</summary>
    public const double TimeConstantSeconds = 1.0;

    /// <summary>Two zero samples in a row end a transfer; a single one may be a pause inside a burst.</summary>
    public const int IdleAfterZeroSamples = 2;

    /// <summary>Below 1 KB/s a fading rate is shown as zero at once, so no "3 B/s" tail lingers.</summary>
    public const double IdleFloorBps = 1000;

    /// <summary>The weight of a new sample <paramref name="elapsed"/> after the previous one.</summary>
    public static double Alpha(TimeSpan elapsed) => 1 - Math.Exp(-elapsed.TotalSeconds / TimeConstantSeconds);

    /// <returns>The next displayed rate, or null when the raw rate is missing.</returns>
    public static SmoothedRate? Next(SmoothedRate? previous, double? raw, double alpha)
    {
        if (raw is not double rate || !double.IsFinite(rate) || rate < 0)
            return null;

        if (previous is not SmoothedRate before)
            return new SmoothedRate(rate, rate == 0 ? 1 : 0);

        if (rate > 0)
            return new SmoothedRate(before.Value + (rate - before.Value) * alpha, 0);

        int zeroRun = before.ZeroRun + 1;
        double faded = before.Value * (1 - alpha);
        return new SmoothedRate(zeroRun >= IdleAfterZeroSamples || faded < IdleFloorBps ? 0 : faded, zeroRun);
    }
}

/// <summary>
/// Smooths the rate of every network interface and drive for display. Totals are sums of these, so
/// they come out smooth too. Immutable: <see cref="Apply"/> returns the shown snapshot and the next state.
/// </summary>
internal sealed record RateSmoother(DateTimeOffset? LastSample, ImmutableDictionary<string, SmoothedRate> Rates)
{
    /// <summary>After a longer gap (a pause, a stalled sensor) every rate starts over from its raw value.</summary>
    public static readonly TimeSpan ResetAfter = TimeSpan.FromSeconds(2);

    public static readonly RateSmoother Empty = new(null, ImmutableDictionary<string, SmoothedRate>.Empty);

    public (SystemSnapshot Shown, RateSmoother Next) Apply(SystemSnapshot raw)
    {
        // Null starts every rate over from its raw value.
        double? alpha = Continues(raw.Timestamp) ? SmoothedRate.Alpha(raw.Timestamp - LastSample!.Value) : null;
        var next = ImmutableDictionary.CreateBuilder<string, SmoothedRate>();

        double? Smooth(string key, double? value)
        {
            SmoothedRate? previous = alpha is not null && Rates.TryGetValue(key, out var known) ? known : null;

            if (SmoothedRate.Next(previous, value, alpha ?? 1) is not SmoothedRate smoothed)
                return null;

            next[key] = smoothed;
            return smoothed.Value;
        }

        var network = WithKeys(raw.Network, n => n.Name, "net")
            .Select(x => x.Item with
            {
                DownBytesPerSec = Smooth($"{x.Key}:down", x.Item.DownBytesPerSec),
                UpBytesPerSec = Smooth($"{x.Key}:up", x.Item.UpBytesPerSec)
            })
            .ToList();

        var storage = WithKeys(raw.Storage, d => d.Name, "disk")
            .Select(x => x.Item with
            {
                ReadBytesPerSec = Smooth($"{x.Key}:read", x.Item.ReadBytesPerSec),
                WriteBytesPerSec = Smooth($"{x.Key}:write", x.Item.WriteBytesPerSec)
            })
            .ToList();

        return (raw with { Network = network, Storage = storage }, new RateSmoother(raw.Timestamp, next.ToImmutable()));
    }

    /// <summary>True when <paramref name="now"/> follows the previous sample closely enough to carry state over.</summary>
    public bool Continues(DateTimeOffset now) =>
        LastSample is DateTimeOffset last && now > last && now - last <= ResetAfter;

    // Two drives or adapters may share a name; the second of a name gets "#1", so each keeps its own state.
    private static IEnumerable<(T Item, string Key)> WithKeys<T>(IReadOnlyList<T> items, Func<T, string> name, string kind) =>
        items.Select((item, index) =>
        {
            int earlier = items.Take(index).Count(other => name(other) == name(item));
            return (item, earlier == 0 ? $"{kind}:{name(item)}" : $"{kind}:{name(item)}#{earlier}");
        });
}
