namespace Vimon.Platform.MacOS.Tests;

public class MacCpuLoadTests
{
    [Fact]
    public void ComputesBusyShareBetweenSamples()
    {
        var previous = new CpuLoadInfo(user: 100, system: 50, idle: 800, nice: 50);
        var current = new CpuLoadInfo(user: 130, system: 60, idle: 850, nice: 60);

        Assert.Equal(50.0, MacCpuLoad.Between(previous, current));
    }

    [Fact]
    public void HandlesWrappingTickCounters()
    {
        var previous = new CpuLoadInfo(user: uint.MaxValue - 9, system: 0, idle: uint.MaxValue - 29, nice: 0);
        var current = new CpuLoadInfo(user: 10, system: 0, idle: 30, nice: 0);

        Assert.Equal(25.0, MacCpuLoad.Between(previous, current));
    }

    [Fact]
    public void ReturnsNullWhenNoTicksElapsed()
    {
        var sample = new CpuLoadInfo(user: 1, system: 2, idle: 3, nice: 4);

        Assert.Null(MacCpuLoad.Between(sample, sample));
    }
}
