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
var theme = ApplyGlyphs(ApplyTheme(cli.Theme), cli.Ascii);

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
bool redrawRequested = false;
(int Width, int Height) drawnSize = (0, 0);
// The widget runs all day in a desktop panel, so it only redraws when there is a new sample.
int redrawIntervalMs = mode == ViewMode.Widget ? sampleIntervalMs : frameIntervalMs;
bool canReadKeys = !Console.IsInputRedirected;
var history = MetricHistory.Empty;
var calm = CalmView.Empty;
SystemSnapshot? snapshot = null;
// What the view shows: smoothed rates and steady process rows (see CalmView); the status check uses the raw snapshot.
SystemSnapshot? shown = null;
long nextSampleAt = 0;

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
        // The view only changes with a new sample, a key or a resized window, so other frames are skipped.
        bool changed = true;

        while (running)
        {
            changed |= SampleIfDue();

            if (changed || CurrentSize() != drawnSize)
                DrawFrame();

            changed = WaitForKeys(redrawIntervalMs);
        }
    }
    finally
    {
        AnsiConsole.Cursor.Show();
    }
}

// Absolute positioning (see ScreenFrame) repairs a resized window with the next frame. Without ANSI
// support the cursor cannot be placed, so frames are simply written one after another.
(int Width, int Height) CurrentSize() => (mode == ViewMode.Widget ? GetWidgetWidth() : GetSafeWidth(), GetWindowHeight());

void DrawFrame()
{
    drawnSize = CurrentSize();
    int width = drawnSize.Width;
    var view = BuildView(width);
    var capabilities = AnsiConsole.Profile.Capabilities;

    if (!capabilities.Ansi)
    {
        AnsiConsole.Write(view);
        AnsiConsole.WriteLine();
        return;
    }

    string rendered = ScreenFrame.Render(view, width, capabilities.ColorSystem, capabilities.Unicode);
    Console.Out.Write(ScreenFrame.Compose(rendered, drawnSize.Height, redrawRequested));
    Console.Out.Flush();
    redrawRequested = false;
}

// Sensors are read every sampleIntervalMs; keys and window size are checked more often so they show quickly.
// Returns true when there is a new sample.
bool SampleIfDue()
{
    long now = Environment.TickCount64;

    if (snapshot is not null && (paused || now < nextSampleAt))
        return false;

    snapshot = ReadSnapshot();
    selectedGpu = snapshot.Gpus.Count == 0 ? 0 : Math.Clamp(selectedGpu, 0, snapshot.Gpus.Count - 1);
    (shown, calm) = calm.Apply(snapshot);
    // The curves follow the shown numbers, so a value and its sparkline never disagree.
    history = history.Append(shown, GpuAt(shown, selectedGpu));
    nextSampleAt = now + sampleIntervalMs;
    return true;
}

IRenderable BuildView(int width)
{
    var current = shown!;
    var gpu = GpuAt(current, selectedGpu);
    var diagnostics = DiagnosticsEngine.Evaluate(snapshot!);

    string clock = current.Timestamp.ToLocalTime().ToString(timestampFormat, CultureInfo.InvariantCulture);

    // A one-line TOP names the busiest process right now, not the first of the steady rows.
    var busiestFirst = current with { TopProcesses = snapshot!.TopProcesses };

    if (mode == ViewMode.Widget)
        return WidgetRenderer.Build(busiestFirst, gpu, diagnostics, history, new WidgetOptions(width, clock, theme));

    if (mode == ViewMode.Compact)
        return new Text(CompactRenderer.Render(busiestFirst, gpu, diagnostics, selectedGpu, width));

    var options = new DashboardOptions(width, hideMissing, clock, paused, theme);
    return Dashboard.Build(current, gpu, diagnostics, selectedGpu, history, options);
}

// Returns true when a key changed the view.
bool WaitForKeys(int budgetMs)
{
    long end = Environment.TickCount64 + budgetMs;

    while (running && Environment.TickCount64 < end)
    {
        if (canReadKeys && Console.KeyAvailable && HandleKey(Console.ReadKey(intercept: true)))
            return true;

        Thread.Sleep(keyPollIntervalMs);
    }

    return false;
}

// Returns true when the view should redraw right away.
bool HandleKey(ConsoleKeyInfo info)
{
    var key = info.Key;

    if (key is ConsoleKey.Q or ConsoleKey.Escape)
    {
        running = false;
        return true;
    }

    // R or Ctrl+L (the usual terminal shortcut) wipe the screen, e.g. after another program wrote into it.
    if (key == ConsoleKey.R || (key == ConsoleKey.L && info.Modifiers.HasFlag(ConsoleModifiers.Control)))
    {
        redrawRequested = true;
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

// --no-processes drops the process names before any view sees them.
SystemSnapshot ReadSnapshot()
{
    // Names come from sources other people control (volume labels, adapter names), so no raw
    // escape sequence may reach the terminal.
    var read = TerminalText.Clean(telemetry.Read());
    return cli.NoProcesses ? read with { TopProcesses = [], TopDiskProcesses = [], ProcessScope = ProcessScope.None } : read;
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

// The classic Windows console shows symbols its font lacks as '?'. Switching Spectre's Unicode
// capability off makes it use square borders; vimon's own symbols follow.
static Theme ApplyGlyphs(Theme theme, bool asciiRequested)
{
    bool legacyConsole = Glyphs.IsLegacyWindowsConsole(
        OperatingSystem.IsWindows(),
        Environment.GetEnvironmentVariable("WT_SESSION"),
        Environment.GetEnvironmentVariable("TERM_PROGRAM"));

    if (!asciiRequested && !legacyConsole && AnsiConsole.Profile.Capabilities.Unicode)
        return theme;

    AnsiConsole.Profile.Capabilities.Unicode = false;
    return theme with { Glyphs = Glyphs.Safe };
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

// 0 when the terminal does not report a height (some pseudo-terminals), so nothing is cut.
static int GetWindowHeight()
{
    try { return Math.Max(0, Console.WindowHeight); }
    catch { return 0; }
}
