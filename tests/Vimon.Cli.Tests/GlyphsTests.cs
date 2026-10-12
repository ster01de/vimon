using Spectre.Console.Testing;
using Vimon.Core;

namespace Vimon.Cli.Tests;

public class GlyphsTests
{
    [Fact]
    public void MapsEverySparklineLevelToAsciiInTheSafeSet()
    {
        Assert.Equal("_.-:=+*#", Glyphs.Safe.Spark("▁▂▃▄▅▆▇█"));
        Assert.Equal("▁▅█", Glyphs.Unicode.Spark("▁▅█"));
    }

    [Theory]
    [InlineData(true, null, null, true)]
    [InlineData(true, "", "", true)]
    [InlineData(true, "4e0b7c-session", null, false)]
    [InlineData(true, null, "vscode", false)]
    [InlineData(false, null, null, false)]
    public void RecognisesTheClassicWindowsConsole(bool isWindows, string? wtSession, string? termProgram, bool expected) =>
        Assert.Equal(expected, Glyphs.IsLegacyWindowsConsole(isWindows, wtSession, termProgram));

    [Fact]
    public void DrawsTheDashboardWithoutSymbolsMissingFromConsoleFonts()
    {
        var snapshot = new SystemSnapshot(
            DateTimeOffset.UnixEpoch,
            new CpuMetrics("Hot CPU", 95, 10, null, null),
            [],
            new MemoryMetrics(4, 16, 25, 5, 20),
            [new StorageMetrics("USB disk", null) { ReadBytesPerSec = 1, WriteBytesPerSec = 1, IsExternal = true, UsbLink = UsbSpeed.High }]);
        var history = MetricHistory.Empty with { CpuTempC = [40, 60, 80] };
        var console = new TestConsole();
        console.Profile.Width = 80;
        console.Profile.Capabilities.Unicode = false;

        console.Write(Dashboard.Build(snapshot, null, DiagnosticsEngine.Evaluate(snapshot), 0, history,
            new DashboardOptions(80, Theme: Theme.Default with { Glyphs = Glyphs.Safe })));

        string output = console.Output;
        foreach (string missing in new[] { "●", "▁", "▅", "█", "░", "↳", "╭", "╰" })
            Assert.DoesNotContain(missing, output);
        Assert.Contains("* CPU 95°C", output);
        Assert.Contains("-> USB 2.0", output);
    }
}
