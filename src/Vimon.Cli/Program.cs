using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;
using Vimon.Cli;
using Vimon.Core;
#if WINDOWS
using Vimon.Platform.Windows;
#else
using Vimon.Platform.Linux;
using Vimon.Platform.MacOS;
#endif

const int sampleIntervalMs = 500;
const int frameIntervalMs = 100;
const int keyPollIntervalMs = 25;
const int onceWarmupMs = 500;
const int onceRetryMs = 250;
const int onceMaxWaitMs = 2000;
const int minWidth = 42;
const string timestampFormat = "HH:mm:ss";

CliOptions cli;

try
{
    cli = CliOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"vimon: {ex.Message}");
    Environment.ExitCode = 2;
    return;
}

using IPlatformTelemetry? telemetry = TryCreateTelemetry();

if (telemetry is null)
{
    Environment.ExitCode = 1;
    return;
}

Console.OutputEncoding = Encoding.UTF8;

bool hideMissing = cli.HideMissing;
var mode = cli.Mode;
var theme = ApplyTheme(cli.Theme);

if (cli.Once)
{
    // CPU load is a delta between two samples. macOS tick counters can stall for ~800 ms, so keep
    // sampling until load shows up instead of trusting a single warm-up interval.
    telemetry.Read();
    Thread.Sleep(onceWarmupMs);

    var first = ReadSnapshot();
    long deadline = Environment.TickCount64 + onceMaxWaitMs;

    while (first.Cpu is not null && first.Cpu.LoadPct is null && Environment.TickCount64 < deadline)
    {
        Thread.Sleep(onceRetryMs);
        first = ReadSnapshot();
    }

    WriteOnce(first);
    return;
}

int selectedGpu = 0;
bool running = true;
bool paused = false;
int frame = 0;
int lastWidth = -1;
// The widget runs all day in a desktop panel, so it only redraws when there is a new sample.
int redrawIntervalMs = mode == ViewMode.Widget ? sampleIntervalMs : frameIntervalMs;
bool canReadKeys = !Console.IsInputRedirected;
var history = MetricHistory.Empty;
SystemSnapshot? snapshot = null;
long nextSampleAt = 0;
IReadOnlyList<string> spinner = AnsiConsole.Profile.Capabilities.Unicode
    ? Spinner.Known.Dots.Frames
    : ["|", "/", "-", "\\"];

// Ctrl+C ends the loop normally so the alternate screen, cursor and sensors are restored and disposed.
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    running = false;
};

// Like top/htop: own screen without scrollback, previous terminal content returns on exit.
if (!Console.IsOutputRedirected && AnsiConsole.Profile.Capabilities.AlternateBuffer)
    AnsiConsole.AlternateScreen(RunLiveView);
else
    RunLiveView();

void RunLiveView()
{
    AnsiConsole.Cursor.Hide();

    try
    {
        AnsiConsole.Live(new Text(""))
            .AutoClear(true)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Bottom)
            .Start(context =>
            {
                while (running)
                {
                    SampleIfDue();
                    context.UpdateTarget(BuildView());
                    context.Refresh();
                    WaitForKeys(redrawIntervalMs);
                    frame++;
                }
            });
    }
    finally
    {
        AnsiConsole.Cursor.Show();
    }
}

// Sensors are read every sampleIntervalMs; the view redraws faster so the spinner animates smoothly.
void SampleIfDue()
{
    long now = Environment.TickCount64;

    if (snapshot is not null && (paused || now < nextSampleAt))
        return;

    snapshot = ReadSnapshot();
    selectedGpu = snapshot.Gpus.Count == 0 ? 0 : Math.Clamp(selectedGpu, 0, snapshot.Gpus.Count - 1);
    history = history.Append(snapshot, GpuAt(snapshot, selectedGpu));
    nextSampleAt = now + sampleIntervalMs;
}

IRenderable BuildView()
{
    var current = snapshot!;
    var gpu = GpuAt(current, selectedGpu);
    var diagnostics = DiagnosticsEngine.Evaluate(current);
    int width = mode == ViewMode.Widget ? GetWidgetWidth() : GetSafeWidth();

    // A narrower window re-wraps the previous frame, so start from a clean screen after a resize.
    if (width != lastWidth)
    {
        AnsiConsole.Clear();
        lastWidth = width;
    }

    if (mode == ViewMode.Widget)
        return WidgetRenderer.Build(current, gpu, diagnostics, history,
            new WidgetOptions(width, current.Timestamp.ToLocalTime().ToString(timestampFormat, CultureInfo.InvariantCulture), theme));

    if (mode == ViewMode.Compact)
        return new Text(CompactRenderer.Render(current, gpu, diagnostics, selectedGpu, width));

    var options = new DashboardOptions(width, hideMissing, spinner[frame % spinner.Count], paused, theme);
    return Dashboard.Build(current, gpu, diagnostics, selectedGpu, history, options);
}

void WaitForKeys(int budgetMs)
{
    long end = Environment.TickCount64 + budgetMs;

    while (running && Environment.TickCount64 < end)
    {
        if (canReadKeys && Console.KeyAvailable && HandleKey(Console.ReadKey(intercept: true).Key))
            return;

        Thread.Sleep(keyPollIntervalMs);
    }
}

// Returns true when the view should redraw right away.
bool HandleKey(ConsoleKey key)
{
    if (key is ConsoleKey.Q or ConsoleKey.Escape)
    {
        running = false;
        return true;
    }

    // The widget has no visible state for pause or GPU selection, so a stray key must not freeze or change it.
    if (mode == ViewMode.Widget)
        return false;

    switch (key)
    {
        case ConsoleKey.P or ConsoleKey.Spacebar:
            paused = !paused;
            return true;
        case ConsoleKey.H:
            hideMissing = !hideMissing;
            return true;
        case ConsoleKey.C:
            mode = CliOptions.ToggleCompact(mode);
            return true;
        case ConsoleKey.RightArrow:
            SelectGpu(selectedGpu + 1);
            return true;
        case ConsoleKey.LeftArrow:
            SelectGpu(selectedGpu - 1);
            return true;
        default:
            return false;
    }
}

void SelectGpu(int index)
{
    int gpuCount = snapshot?.Gpus.Count ?? 0;
    int clamped = gpuCount == 0 ? 0 : Math.Clamp(index, 0, gpuCount - 1);

    if (clamped == selectedGpu)
        return;

    selectedGpu = clamped;
    history = history.WithoutGpu();
}

// --no-processes drops the names before any view sees them.
SystemSnapshot ReadSnapshot()
{
    var read = telemetry.Read();
    return cli.NoProcesses ? read with { TopProcesses = [], ProcessScope = ProcessScope.None } : read;
}

void WriteOnce(SystemSnapshot s)
{
    var gpu = GpuAt(s, 0);
    var diagnostics = DiagnosticsEngine.Evaluate(s);

    switch (mode)
    {
        // Compact one-shot output is meant for status bars and scripts, so it is never cut to the window.
        case ViewMode.Compact:
            Console.Write(CompactRenderer.Render(s, gpu, diagnostics, 0, maxWidth: null));
            break;
        case ViewMode.Widget:
            AnsiConsole.Write(WidgetRenderer.Build(s, gpu, diagnostics, MetricHistory.Empty,
                new WidgetOptions(Console.IsOutputRedirected ? WidgetRenderer.MaxWidth : GetWidgetWidth(), Theme: theme)));
            break;
        default:
            AnsiConsole.Write(Dashboard.Build(
                s, gpu, diagnostics, 0, MetricHistory.Empty,
                new DashboardOptions(AnsiConsole.Profile.Width, hideMissing, Theme: theme)));
            break;
    }
}

// Spectre has already turned colour off for NO_COLOR, TERM=dumb and redirected output; an explicit
// --theme switches it back on, but only where the terminal understands ANSI escape codes at all.
static Theme ApplyTheme(Theme? requested)
{
    var capabilities = AnsiConsole.Profile.Capabilities;
    var (theme, forceColor) = ThemeResolver.Resolve(
        requested, consoleHasColor: capabilities.ColorSystem != ColorSystem.NoColors, canForceColor: capabilities.Ansi);

    // 16 colours are enough for every theme and work in every ANSI terminal.
    if (forceColor)
        capabilities.ColorSystem = ColorSystem.Standard;

    return theme;
}

static GpuMetrics? GpuAt(SystemSnapshot s, int index) =>
    s.Gpus.Count > 0 ? s.Gpus[Math.Clamp(index, 0, s.Gpus.Count - 1)] : null;

static IPlatformTelemetry? TryCreateTelemetry()
{
    try
    {
        return CreateTelemetry();
    }
    catch (PlatformNotSupportedException ex)
    {
        Console.Error.WriteLine($"vimon: {ex.Message}");
        return null;
    }
}

static IPlatformTelemetry CreateTelemetry()
{
#if WINDOWS
    return new WindowsTelemetry();
#else
    if (OperatingSystem.IsMacOS())
        return new MacTelemetry();

    if (OperatingSystem.IsLinux())
        return new LinuxTelemetry();

    throw new PlatformNotSupportedException(
        $"{RuntimeInformation.OSDescription} is not supported by this build. Use the Windows release on Windows.");
#endif
}

static int GetSafeWidth()
{
    try { return Math.Max(minWidth, Console.WindowWidth - 1); }
    catch { return 80; }
}

// Desktop panels are often only as wide as the widget, so the width follows the window down to a minimum.
static int GetWidgetWidth()
{
    try { return Math.Clamp(Console.WindowWidth - 1, WidgetRenderer.MinWidth, WidgetRenderer.MaxWidth); }
    catch { return WidgetRenderer.MaxWidth; }
}
