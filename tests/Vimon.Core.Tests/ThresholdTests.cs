using Vimon.Core;

namespace Vimon.Core.Tests;

public class ThresholdTests
{
    [Theory]
    [InlineData(79.9, HealthLevel.Good)]
    [InlineData(80.0, HealthLevel.Warning)]
    [InlineData(89.9, HealthLevel.Warning)]
    [InlineData(90.0, HealthLevel.Critical)]
    public void ClassifiesValuesAtAndAboveEachLimit(double value, HealthLevel expected) =>
        Assert.Equal(expected, new Threshold(Warning: 80, Critical: 90).Classify(value));

    [Fact]
    public void DiagnosticsUseTheGivenThresholds()
    {
        var thresholds = HealthThresholds.Default with { CpuTempC = new Threshold(60, 70) };
        var snapshot = new SystemSnapshot(
            DateTimeOffset.UnixEpoch,
            new CpuMetrics("Test CPU", 65, 10, null, null),
            [],
            new MemoryMetrics(4, 32, 12.5, 6, 40),
            []);

        var diagnostic = Assert.Single(DiagnosticsEngine.Evaluate(snapshot, thresholds));

        Assert.Equal("CPU_WARM", diagnostic.Code);
    }
}
