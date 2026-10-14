using Vimon.Core;

namespace Vimon.Cli.Tests;

/// <summary>Displayed rates follow the traffic calmly and still show idle as soon as it stops.</summary>
public class RateSmootherTests
{
    private static readonly MemoryMetrics Memory = new(4, 16, 25, 5, 20);

    [Fact]
    public void AlphaFollowsTheSampleInterval()
    {
        Assert.Equal(1 - Math.Exp(-0.5), SmoothedRate.Alpha(TimeSpan.FromSeconds(0.5)), precision: 9);
        Assert.Equal(1 - Math.Exp(-1), SmoothedRate.Alpha(TimeSpan.FromSeconds(1)), precision: 9);
    }

    [Fact]
    public void StartsWithTheFirstRawValue() => Assert.Equal(1_000, Down(Run((0, 1_000))));

    [Fact]
    public void MovesTowardANewValueByAlpha()
    {
        double alpha = SmoothedRate.Alpha(TimeSpan.FromSeconds(0.5));

        Assert.Equal(10_000 + 10_000 * alpha, Down(Run((0, 10_000), (0.5, 20_000)))!.Value, precision: 6);
    }

    [Fact]
    public void ShowsNoIdleForASingleZeroSample() => Assert.True(Down(Run((0, 1_000_000), (0.5, 0))) > 0);

    [Fact]
    public void ShowsIdleAfterTwoZeroSamples() => Assert.Equal(0, Down(Run((0, 1_000_000), (0.5, 0), (1.0, 0))));

    [Fact]
    public void ShowsIdleAtOnceWhenTheFadingRateIsTiny() => Assert.Equal(0, Down(Run((0, 1_500), (0.5, 0))));

    [Fact]
    public void ShowsNothingWhileTheRawRateIsMissing() => Assert.Null(Down(Run((0, 1_000), (0.5, null))));

    [Fact]
    public void StartsOverAfterALongGap() => Assert.Equal(50_000, Down(Run((0, 1_000), (10, 50_000))));

    [Fact]
    public void SmoothsDrivesLikeInterfaces()
    {
        var (_, state) = RateSmoother.Empty.Apply(Drive(0, 10_000));
        var (shown, _) = state.Apply(Drive(0.5, 20_000));

        Assert.True(shown.Storage[0].ReadBytesPerSec is > 10_000 and < 20_000);
    }

    [Fact]
    public void ForgetsInterfacesThatAreGone()
    {
        var (_, state) = RateSmoother.Empty.Apply(Sample(0, 1_000));
        var (_, next) = state.Apply(Sample(0.5, 1_000) with { Network = [new InterfaceRate("en1", 5, 5)] });

        Assert.DoesNotContain(next.Rates.Keys, key => key.Contains("en0", StringComparison.Ordinal));
    }

    [Fact]
    public void LeavesTheRawSnapshotAsItIs()
    {
        var (_, state) = RateSmoother.Empty.Apply(Sample(0, 10_000));
        var raw = Sample(0.5, 20_000);

        state.Apply(raw);

        Assert.Equal(20_000, raw.Network[0].DownBytesPerSec);
    }

    private static SystemSnapshot Sample(double seconds, double? down) =>
        new SystemSnapshot(DateTimeOffset.UnixEpoch.AddSeconds(seconds), null, [], Memory, [])
        {
            Network = [new InterfaceRate("en0", down, down)]
        };

    private static SystemSnapshot Drive(double seconds, double rate) =>
        new(DateTimeOffset.UnixEpoch.AddSeconds(seconds), null, [], Memory,
            [new StorageMetrics("SSD", null) { ReadBytesPerSec = rate, WriteBytesPerSec = rate }]);

    private static SystemSnapshot Run(params (double Seconds, double? Down)[] samples)
    {
        var state = RateSmoother.Empty;
        SystemSnapshot shown = null!;

        foreach (var (seconds, down) in samples)
            (shown, state) = state.Apply(Sample(seconds, down));

        return shown;
    }

    private static double? Down(SystemSnapshot shown) => shown.Network[0].DownBytesPerSec;
}
