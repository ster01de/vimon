using System.Runtime.InteropServices;
using System.Text;
using Vimon.Cli;
using Vimon.Core;
#if WINDOWS
using Vimon.Platform.Windows;
#else
using Vimon.Platform.Linux;
using Vimon.Platform.MacOS;
#endif

const int refreshIntervalMs = 500;
const int keyPollIntervalMs = 25;
const int onceWarmupMs = 500;

using IPlatformTelemetry? telemetry = TryCreateTelemetry();

if (telemetry is null)
{
    Environment.ExitCode = 1;
    return;
}

Console.OutputEncoding = Encoding.UTF8;

bool hideMissing = args.Contains("--hide-missing");

if (args.Contains("--once"))
{
    // Some values (e.g. CPU load) are deltas between two samples, so take a warm-up sample first.
    telemetry.Read();
    Thread.Sleep(onceWarmupMs);

    var snapshot = telemetry.Read();
    var gpu = snapshot.Gpus.Count > 0 ? snapshot.Gpus[0] : null;

    Console.Write(FrameRenderer.Render(
        snapshot, gpu, DiagnosticsEngine.Evaluate(snapshot), 0, GetSafeWidth(), "-", hideMissing));
    return;
}

int selectedGpu = 0;
bool running = true;
bool paused = false;
int frame = 0;
int lastWidth = -1;
int lastHeight = -1;
int lastLineCount = 0;
bool canReadKeys = !Console.IsInputRedirected;
string[] spinner = ["|", "/", "-", "\\"];

// Ctrl+C ends the loop normally so the finally block restores the cursor and disposes the sensors.
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    running = false;
};

bool useAlternateScreen = TerminalScreen.ShouldUseAlternateScreen(
    OperatingSystem.IsWindows(), Console.IsOutputRedirected);

if (useAlternateScreen)
    Console.Write(TerminalScreen.EnterAlternateScreen);

Console.CursorVisible = false;

try
{
    while (running)
    {
        if (!paused)
        {
            var snapshot = telemetry.Read();

            selectedGpu = snapshot.Gpus.Count == 0
                ? 0
                : Math.Clamp(selectedGpu, 0, snapshot.Gpus.Count - 1);

            var gpu = snapshot.Gpus.Count > 0 ? snapshot.Gpus[selectedGpu] : null;
            var diagnostics = DiagnosticsEngine.Evaluate(snapshot);

            int width = GetSafeWidth();
            int height = GetSafeHeight();

            if (width != lastWidth || height != lastHeight)
            {
                TryClear();
                lastWidth = width;
                lastHeight = height;
                lastLineCount = 0;
            }
            else
            {
                TryHome();
            }

            string text = FrameRenderer.Render(
                snapshot,
                gpu,
                diagnostics,
                selectedGpu,
                width,
                spinner[frame++ % spinner.Length],
                hideMissing);

            var fitted = TerminalFrame.Fit(text, height, lastLineCount);
            Console.Write(fitted.Text);
            lastLineCount = fitted.LineCount;
        }

        var end = DateTime.UtcNow.AddMilliseconds(refreshIntervalMs);
        while (DateTime.UtcNow < end && running)
        {
            if (canReadKeys && Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;

                if (key is ConsoleKey.Q or ConsoleKey.Escape)
                    running = false;
                else if (key is ConsoleKey.P or ConsoleKey.Spacebar)
                    paused = !paused;
                else if (key == ConsoleKey.H)
                {
                    hideMissing = !hideMissing;
                    end = DateTime.UtcNow; // redraw right away instead of after the refresh interval
                }
                else if (key == ConsoleKey.RightArrow)
                    selectedGpu++;
                else if (key == ConsoleKey.LeftArrow)
                    selectedGpu = Math.Max(0, selectedGpu - 1);
            }

            Thread.Sleep(keyPollIntervalMs);
        }
    }
}
finally
{
    Console.CursorVisible = true;

    // Leaving the alternate screen restores the terminal content from before vimon started.
    if (useAlternateScreen)
        Console.Write(TerminalScreen.LeaveAlternateScreen);
    else
        TryClear();
}

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
    try { return Math.Max(FrameRenderer.MinWidth, Console.WindowWidth - 1); }
    catch { return 80; }
}

static int GetSafeHeight()
{
    try { return Math.Max(1, Console.WindowHeight); }
    catch { return 25; }
}

static void TryHome()
{
    try
    {
        if (!Console.IsOutputRedirected)
            Console.SetCursorPosition(0, 0);
    }
    catch { }
}

static void TryClear()
{
    try
    {
        if (!Console.IsOutputRedirected)
            Console.Clear();
    }
    catch { }
}
