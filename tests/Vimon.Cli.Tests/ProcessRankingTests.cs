using Vimon.Core;

namespace Vimon.Cli.Tests;

/// <summary>Process rows keep their place unless a process is clearly busier.</summary>
public class ProcessRankingTests
{
    [Fact]
    public void KeepsTheBackendOrderOnTheFirstReading() =>
        Assert.Equal(new[] { 2, 1 }, Pids(ProcessRanking.Arrange([], [P(2, 40), P(1, 30)])));

    [Fact]
    public void KeepsTheOrderWithinTheMargin() =>
        Assert.Equal(new[] { 1, 2 }, Pids(ProcessRanking.Arrange([P(1, 30), P(2, 28)], [P(2, 32), P(1, 30)])));

    [Fact]
    public void ReordersWhenAProcessIsClearlyAhead() =>
        Assert.Equal(new[] { 2, 1 }, Pids(ProcessRanking.Arrange([P(1, 30), P(2, 28)], [P(2, 40), P(1, 30)])));

    [Fact]
    public void ReordersWhenAProcessIsClearlyAheadOfAnyRowAbove() =>
        Assert.Equal(new[] { 4, 3, 2, 1 }, Pids(ProcessRanking.Arrange(
            [P(1, 10), P(2, 14), P(3, 18), P(4, 22)],
            [P(4, 22), P(3, 18), P(2, 14), P(1, 10)])));

    [Fact]
    public void GivesTheRowOfALeavingProcessToANewcomer() =>
        Assert.Equal(new[] { 1, 4, 3 }, Pids(ProcessRanking.Arrange(
            [P(1, 30), P(2, 20), P(3, 18)],
            [P(1, 30), P(3, 19), P(4, 17)])));

    [Fact]
    public void ClosesRowsNobodyTakes() =>
        Assert.Equal(new[] { 1, 3 }, Pids(ProcessRanking.Arrange([P(1, 30), P(2, 20), P(3, 18)], [P(1, 30), P(3, 18)])));

    [Fact]
    public void ChangesNothingWhenTheSameReadingComesAgain()
    {
        IReadOnlyList<ProcessUsage> current = [P(2, 32), P(1, 30), P(3, 5)];
        var once = ProcessRanking.Arrange([P(1, 30), P(2, 28)], current);

        Assert.Equal(Pids(once), Pids(ProcessRanking.Arrange(once, current)));
    }

    [Fact]
    public void TreatsAReusedPidWithAnotherNameAsANewProcess()
    {
        var rows = ProcessRanking.Arrange([P(1, 30)], [new ProcessUsage(1, "other", 10, 0)]);

        Assert.Equal("other", Assert.Single(rows).Name);
    }

    [Fact]
    public void KeepsTheBusiestUntilAnotherMovesOneAndAHalfTimesAsMuch()
    {
        var previous = D(1, 100);

        Assert.Equal(1, ProcessRanking.KeepBusiest(previous, [D(2, 140), D(1, 100)])[0].Pid);
        Assert.Equal(2, ProcessRanking.KeepBusiest(previous, [D(2, 150), D(1, 100)])[0].Pid);
    }

    [Fact]
    public void SwitchesTheBusiestWhenItStopsOrLeaves()
    {
        var previous = D(1, 100);

        Assert.Equal(2, ProcessRanking.KeepBusiest(previous, [D(2, 10), D(1, 0)])[0].Pid);
        Assert.Equal(2, ProcessRanking.KeepBusiest(previous, [D(2, 10)])[0].Pid);
    }

    private static ProcessUsage P(int pid, double cpu) => new(pid, $"proc{pid}", cpu, 0);

    private static ProcessUsage D(int pid, double bytesPerSec) =>
        new ProcessUsage(pid, $"proc{pid}", 1, 0) { DiskReadBytesPerSec = bytesPerSec, DiskWriteBytesPerSec = 0 };

    private static int[] Pids(IEnumerable<ProcessUsage> rows) => rows.Select(p => p.Pid).ToArray();
}
