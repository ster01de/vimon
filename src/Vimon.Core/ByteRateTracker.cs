using System.Collections.Immutable;

namespace Vimon.Core;

/// <summary>Cumulative byte counters of one network interface at one moment.</summary>
public sealed record ByteCounters(string Name, ulong InBytes, ulong OutBytes);

/// <summary>
/// Turns cumulative byte counters into bytes per second. Immutable: <see cref="Advance"/> returns
/// the rates together with the tracker for the next sample.
/// </summary>
public sealed class ByteRateTracker
{
    public static readonly ByteRateTracker Empty = new(ImmutableDictionary<string, ByteCounters>.Empty, sampledAt: null);

    private readonly ImmutableDictionary<string, ByteCounters> _previous;
    private readonly TimeSpan? _sampledAt;

    private ByteRateTracker(ImmutableDictionary<string, ByteCounters> previous, TimeSpan? sampledAt)
    {
        _previous = previous;
        _sampledAt = sampledAt;
    }

    /// <param name="now">Monotonic time of this sample, e.g. <c>Stopwatch.GetElapsedTime(start)</c>.</param>
    /// <returns>
    /// One rate per counter, in input order. A direction is null on the first sample of a name, when
    /// no time has passed, or when its counter went backwards (reset or wrap).
    /// </returns>
    public (IReadOnlyList<InterfaceRate> Rates, ByteRateTracker Next) Advance(IReadOnlyList<ByteCounters> counters, TimeSpan now)
    {
        double? seconds = _sampledAt is TimeSpan before && now > before ? (now - before).TotalSeconds : null;
        var rates = counters.Select(current => Rate(current, seconds)).ToList();

        // A name can repeat if the OS lists an interface twice; the last entry wins.
        var next = counters
            .GroupBy(c => c.Name)
            .ToImmutableDictionary(group => group.Key, group => group.Last());

        return (rates, new ByteRateTracker(next, now));
    }

    private InterfaceRate Rate(ByteCounters current, double? seconds)
    {
        if (seconds is not double elapsed || !_previous.TryGetValue(current.Name, out var previous))
            return new InterfaceRate(current.Name, null, null);

        return new InterfaceRate(
            current.Name,
            PerSecond(previous.InBytes, current.InBytes, elapsed),
            PerSecond(previous.OutBytes, current.OutBytes, elapsed));
    }

    private static double? PerSecond(ulong before, ulong after, double seconds) =>
        after >= before ? (after - before) / seconds : null;
}
