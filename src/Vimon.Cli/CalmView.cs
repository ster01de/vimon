using Vimon.Core;

namespace Vimon.Cli;

/// <summary>
/// Turns each raw sample into the one the live view shows: smoothed rates and process rows that keep
/// their place. Display only; the status check keeps using the raw sample. Immutable.
/// </summary>
internal sealed record CalmView(RateSmoother Rates, IReadOnlyList<ProcessUsage> Processes, ProcessUsage? Busiest)
{
    public static readonly CalmView Empty = new(RateSmoother.Empty, [], null);

    public (SystemSnapshot Shown, CalmView Next) Apply(SystemSnapshot raw)
    {
        // After a pause the old order means nothing any more, so rows start from the backend's order.
        bool continues = Rates.Continues(raw.Timestamp);
        var (smoothed, rates) = Rates.Apply(raw);
        var processes = ProcessRanking.Arrange(continues ? Processes : [], raw.TopProcesses);
        var disk = ProcessRanking.KeepBusiest(continues ? Busiest : null, raw.TopDiskProcesses);

        var shown = smoothed with { TopProcesses = processes, TopDiskProcesses = disk };
        return (shown, new CalmView(rates, processes, disk.Count > 0 ? disk[0] : null));
    }
}
