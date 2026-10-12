using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Testing;
using Vimon.Core;

namespace Vimon.Cli.Tests;

public class WidgetRendererTests
{
    private static readonly MemoryMetrics Memory = new(4, 16, 25, 5, 20);

    private static SystemSnapshot Snapshot(CpuMetrics? cpu = null, params GpuMetrics[] gpus) =>
        new(DateTimeOffset.UnixEpoch, cpu ?? Cpu(50, 10), gpus, Memory, []);

    private static CpuMetrics Cpu(double? tempC, double? loadPct) => new("Test CPU", tempC, loadPct, null, null);

    private static GpuMetrics Gpu(double? coreTempC = 60, double? loadPct = 30) =>
        new(0, "Test GPU", Vendor.Nvidia,
            coreTempC, null, null, loadPct, null, null, null, null, null, null,
            null, null, null, null);

    private static TestConsole Console(bool ansi)
    {
        var console = new TestConsole();
        console.Profile.Width = 120;
        console.Profile.Capabilities.ColorSystem = ansi ? ColorSystem.Standard : ColorSystem.NoColors;
        console.EmitAnsiSequences = ansi;
        return console;
    }

    private static string Render(
        SystemSnapshot snapshot,
        int width = WidgetRenderer.MaxWidth,
        MetricHistory? history = null,
        string? timestamp = null,
        bool ansi = false,
        Theme? theme = null)
    {
        var console = Console(ansi);
        console.Write(WidgetRenderer.Build(
            snapshot,
            snapshot.Gpus.FirstOrDefault(),
            DiagnosticsEngine.Evaluate(snapshot),
            history ?? MetricHistory.Empty,
            new WidgetOptions(width, timestamp, theme)));

        return console.Output;
    }

    private static int[] SgrParameters(string output) =>
        Regex.Matches(output, @"\x1b\[([0-9;]*)m")
            .SelectMany(m => m.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Select(int.Parse)
            .ToArray();

    public static TheoryData<string> ThemeNames => new(Theme.All.Select(theme => theme.Name).ToArray());

    private static string[] Lines(string output) =>
        output.Split('\n')
            .Select(line => line.TrimEnd('\r').TrimEnd())
            .Where(line => line.Length > 0)
            .ToArray();

    // The escape sequence Spectre writes for a colour in the same console setup, so tests never hard-code SGR codes.
    private static string AnsiPrefix(string color)
    {
        var console = Console(ansi: true);
        console.Write(new Markup($"[{color}]X[/]"));
        return console.Output[..console.Output.IndexOf('X')];
    }

    [Fact]
    public void RendersStatusCpuGpuAndRamInOrder()
    {
        var lines = Lines(Render(Snapshot(gpus: Gpu())));

        Assert.StartsWith("OK", lines[0]);
        Assert.StartsWith("CPU", lines[1]);
        Assert.StartsWith("GPU", lines[2]);
        Assert.StartsWith("RAM", lines[3]);
    }

    [Fact]
    public void HasNoBordersHeaderOrKeyHelp()
    {
        string output = Render(Snapshot(gpus: Gpu()));

        foreach (string forbidden in new[] { "╭", "│", "─", "VIMON", "quit" })
            Assert.DoesNotContain(forbidden, output);
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void EmitsNoBackgroundColoursSoTransparencyShowsThrough(string themeName)
    {
        var hot = Snapshot(Cpu(tempC: 95, loadPct: 90), Gpu(coreTempC: 84)) with { Thermal = ThermalState.Serious };
        var history = MetricHistory.Empty with { CpuLoadPct = [10, 50, 90] };

        string output = Render(hot, history: history, timestamp: "12:00:00", ansi: true, theme: Theme.Find(themeName));

        var parameters = SgrParameters(output);
        Assert.NotEmpty(parameters);
        Assert.DoesNotContain(parameters, p => p is >= 40 and <= 49 or >= 100 and <= 107);
    }

    [Fact]
    public void MonoMarksCriticalWithBoldInsteadOfColour()
    {
        string output = Render(Snapshot(Cpu(tempC: 95, loadPct: 10)), ansi: true, theme: Theme.Mono);

        var parameters = SgrParameters(output);
        Assert.Contains(1, parameters);
        Assert.DoesNotContain(parameters, p => p is >= 30 and <= 38 or >= 90 and <= 97);
    }

    [Fact]
    public void HighContrastUsesItsOwnCriticalStyle()
    {
        string output = Render(Snapshot(Cpu(tempC: 95, loadPct: 10)), ansi: true, theme: Theme.HighContrast);

        Assert.StartsWith(AnsiPrefix(Theme.HighContrast.Critical) + "CRIT", output);
    }

    [Fact]
    public void ColoursStatusByWorstLevel()
    {
        string output = Render(Snapshot(Cpu(tempC: 95, loadPct: 10)), ansi: true);

        Assert.StartsWith(AnsiPrefix("red") + "CRIT", output);
    }

    [Fact]
    public void ColoursTemperatureByThreshold()
    {
        string output = Render(Snapshot(Cpu(tempC: 85, loadPct: 10)), ansi: true);

        Assert.Contains(AnsiPrefix("yellow") + " 85°C", output);
    }

    [Fact]
    public void ShowsAtMostTwoFindingsWorstFirst()
    {
        var snapshot = Snapshot(Cpu(tempC: 85, loadPct: 10), Gpu(coreTempC: 90))
            with { Thermal = ThermalState.Serious };

        var lines = Lines(Render(snapshot));

        Assert.Equal("  Test GPU: GPU 90°C", lines[1]);
        Assert.StartsWith("  CPU 85°C", lines[2]);
        Assert.StartsWith("CPU", lines[3]);
    }

    [Fact]
    public void LeavesOutTheHealthyMessage()
    {
        string output = Render(Snapshot());

        Assert.DoesNotContain("No important issues", output);
    }

    [Fact]
    public void KeepsEveryLineWithinTheWidth()
    {
        var snapshot = Snapshot(Cpu(tempC: 95, loadPct: 10), Gpu(coreTempC: 90))
            with
            {
                Thermal = ThermalState.Serious,
                Memory = Memory with { Pressure = MemoryPressure.Warning },
                Network = [new("en0", 999_000_000, 999_000)]
            };
        var history = MetricHistory.Empty with
        {
            CpuLoadPct = [.. Enumerable.Range(0, 20).Select(i => i * 5.0)],
            NetDownBps = [.. Enumerable.Range(0, 20).Select(i => i * 50_000.0)]
        };

        foreach (int width in new[] { WidgetRenderer.MinWidth, 22, WidgetRenderer.MaxWidth })
        {
            var lines = Lines(Render(snapshot, width, history, timestamp: "12:00:00"));

            Assert.All(lines, line => Assert.True(line.Length <= width, $"'{line}' is wider than {width}"));
        }
    }

    [Fact]
    public void CutsLongFindingsWithAnEllipsis()
    {
        var lines = Lines(Render(Snapshot() with { Thermal = ThermalState.Serious }));

        Assert.EndsWith("…", lines[1]);
    }

    [Fact]
    public void OmitsTheGpuRowWithoutGpu()
    {
        string output = Render(Snapshot());

        Assert.DoesNotContain("GPU", output);
    }

    [Fact]
    public void LeavesOutMissingValuesInsteadOfDashes()
    {
        var lines = Lines(Render(Snapshot(Cpu(tempC: null, loadPct: 10), Gpu(coreTempC: null, loadPct: null))));

        Assert.DoesNotContain(lines, line => line.Contains(Dashboard.Missing));
        Assert.DoesNotContain(lines, line => line.StartsWith("GPU"));
        // The empty temperature column keeps the load aligned with the rows below.
        Assert.Equal("CPU" + new string(' ', 9) + "10%", lines.Single(line => line.StartsWith("CPU")));
    }

    [Fact]
    public void DrawsOnlyTheMostRecentLoadSamples()
    {
        var history = MetricHistory.Empty with { CpuLoadPct = [.. Enumerable.Repeat(0.0, 10), .. Enumerable.Repeat(100.0, 10)] };

        string cpuLine = Lines(Render(Snapshot(), history: history)).Single(line => line.StartsWith("CPU"));

        Assert.EndsWith(new string('█', WidgetRenderer.SparkLength), cpuLine);
    }

    [Fact]
    public void DropsSparklinesWhenNarrow()
    {
        var history = MetricHistory.Empty with { CpuLoadPct = [10, 50, 90] };

        string output = Render(Snapshot(), width: WidgetRenderer.MinWidth, history: history);

        Assert.DoesNotContain("█", output);
    }

    [Fact]
    public void ShowsPressureAndThermalOnlyWhenReported()
    {
        var reported = Snapshot() with { Thermal = ThermalState.Nominal, Memory = Memory with { Pressure = MemoryPressure.Normal } };

        Assert.DoesNotContain("Pressure", Render(Snapshot()));
        Assert.DoesNotContain("Thermal", Render(Snapshot()));
        Assert.Contains("Pressure  Normal", Render(reported));
        Assert.Contains("Thermal   Nominal", Render(reported));
    }

    [Fact]
    public void ShowsNetworkThroughputBelowRam()
    {
        var lines = Lines(Render(Snapshot() with { Network = [new("en0", 1_200_000, 310_000)] }));

        int ram = Array.FindIndex(lines, line => line.StartsWith("RAM"));
        Assert.Equal($"NET ↓{1.2:0.0}M ↑310K", lines[ram + 1]);
    }

    [Fact]
    public void LeavesOutTheNetworkRowUntilARateIsKnown()
    {
        string output = Render(Snapshot() with { Network = [new("en0", null, null)] });

        Assert.DoesNotContain("NET", output);
    }

    [Fact]
    public void SurvivesANonFiniteRamPercentage()
    {
        var broken = Snapshot() with { Memory = Memory with { LoadPct = double.NaN } };

        var lines = Lines(Render(broken));

        Assert.Contains(lines, line => line.StartsWith("RAM"));
    }

    [Fact]
    public void ShowsTheTimestampOnlyWhenGiven()
    {
        Assert.DoesNotContain(":", Lines(Render(Snapshot()))[0]);
        Assert.EndsWith("12:34:56", Lines(Render(Snapshot(), timestamp: "12:34:56"))[0]);
    }
}
