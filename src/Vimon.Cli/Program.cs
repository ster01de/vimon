using System.Runtime.InteropServices;
using System.Text;
using Vimon.Core;
#if WINDOWS
using Vimon.Platform.Windows;
#else
using Vimon.Platform.Linux;
using Vimon.Platform.MacOS;
#endif

using IPlatformTelemetry? telemetry = TryCreateTelemetry();

if (telemetry is null)
{
    Environment.ExitCode = 1;
    return;
}

Console.OutputEncoding = Encoding.UTF8;
Console.CursorVisible = false;

int selectedGpu = 0;
bool running = true;
bool paused = false;
int frame = 0;
int lastWidth = -1;
int lastHeight = -1;
string[] spinner = ["|", "/", "-", "\\"];

try
{
    while (running)
    {
        if (!paused)
        {
            var snapshot = telemetry.Read();

            if (snapshot.Gpus.Count == 0)
                selectedGpu = 0;
            else
                selectedGpu = Math.Clamp(selectedGpu, 0, snapshot.Gpus.Count - 1);

            var gpu = snapshot.Gpus.Count > 0 ? snapshot.Gpus[selectedGpu] : null;
            var diagnostics = DiagnosticsEngine.Evaluate(snapshot);

            int width = GetSafeWidth();
            int height = GetSafeHeight();

            if (width != lastWidth || height != lastHeight)
            {
                TryClear();
                lastWidth = width;
                lastHeight = height;
            }
            else
            {
                TryHome();
            }

            Console.Write(Render(
                snapshot,
                gpu,
                diagnostics,
                selectedGpu,
                width,
                spinner[frame++ % spinner.Length]));
        }

        var end = DateTime.UtcNow.AddMilliseconds(500);
        while (DateTime.UtcNow < end && running)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;

                if (key is ConsoleKey.Q or ConsoleKey.Escape)
                    running = false;
                else if (key is ConsoleKey.P or ConsoleKey.Spacebar)
                    paused = !paused;
                else if (key == ConsoleKey.RightArrow)
                    selectedGpu++;
                else if (key == ConsoleKey.LeftArrow)
                    selectedGpu = Math.Max(0, selectedGpu - 1);
            }

            Thread.Sleep(25);
        }
    }
}
finally
{
    Console.CursorVisible = true;
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

static string Render(
    SystemSnapshot s,
    GpuMetrics? gpu,
    IReadOnlyList<Diagnostic> diagnostics,
    int selectedGpu,
    int width,
    string spin)
{
    int w = Math.Clamp(width, 42, 96);
    var b = new StringBuilder();

    void Border() =>
        b.Append('+').Append(new string('-', w - 2)).AppendLine("+");

    void Row(string text = "")
    {
        string t = " " + text;
        int inner = w - 2;

        if (t.Length > inner)
            t = t[..inner];

        b.Append('|').Append(t.PadRight(inner)).AppendLine("|");
    }

    string Short(string value, int max) =>
        value.Length <= max ? value : value[..Math.Max(1, max - 1)] + "…";

    Border();
    Row($"VIMON  {spin} LIVE");
    Row("Q quit  P pause  <- / -> GPU");
    Border();

    Row("CPU");
    Row(Short(s.Cpu?.Name ?? "Unknown CPU", w - 4));
    Row($"Temp        {FmtMetric(s.Cpu?.TempC, " C", positiveOnly: true)}");
    Row($"Load        {FmtMetric(s.Cpu?.LoadPct, " %")}");
    Row($"Power       {FmtMetric(s.Cpu?.PackagePowerW, " W", positiveOnly: true)}");
    Border();

    Row($"GPU {(s.Gpus.Count == 0 ? "0/0" : $"{selectedGpu + 1}/{s.Gpus.Count}")}");

    if (gpu is null)
    {
        Row("No GPU detected");
    }
    else
    {
        Row(Short(gpu.Name, w - 4));
        Row($"Core        {FmtMetric(gpu.CoreTempC, " C", positiveOnly: true)}");
        Row($"Hotspot     {FmtMetric(gpu.HotspotTempC, " C", positiveOnly: true)}");
        Row($"VRAM temp   {FmtMetric(gpu.MemoryTempC, " C", positiveOnly: true)}");
        Row($"Load        {FmtMetric(gpu.UtilizationPct, " %")}");
        Row($"Power       {FmtMetric(gpu.PowerW, " W", positiveOnly: true)}");
        Row($"Fan         {FmtMetric(gpu.FanPct, " %")}");
        Row($"PCIe        {Pcie(gpu)}");
    }

    Border();

    Row("MEMORY");
    Row($"RAM         {s.Memory.UsedGb:0.0} / {s.Memory.TotalGb:0.0} GB");
    Row($"Usage       {s.Memory.LoadPct:0} %");
    Border();

    Row("STORAGE");
    if (s.Storage.Count == 0)
    {
        Row("Temperature —");
    }
    else
    {
        foreach (var drive in s.Storage.Take(3))
            Row($"{Short(drive.Name, Math.Max(12, w - 20))}  {FmtMetric(drive.TempC, " C", positiveOnly: true)}");
    }

    Border();

    Row("STATUS");
    foreach (var diagnostic in diagnostics.Take(3))
        Row(Short($"{diagnostic.Level}: {diagnostic.Message}", w - 4));

    Border();

    return b.ToString();
}

static string FmtMetric(double? value, string unit, bool positiveOnly = false)
{
    if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        return "—";

    if (positiveOnly && value.Value <= 0)
        return "—";

    return $"{value.Value:0.#}{unit}";
}

static string Pcie(GpuMetrics gpu)
{
    if (!gpu.PcieGenerationCurrent.HasValue || !gpu.PcieWidthCurrent.HasValue)
        return "—";

    string current = $"Gen{gpu.PcieGenerationCurrent} x{gpu.PcieWidthCurrent}";

    if (gpu.PcieGenerationMax.HasValue && gpu.PcieWidthMax.HasValue)
        return $"{current} / max Gen{gpu.PcieGenerationMax} x{gpu.PcieWidthMax}";

    return current;
}

static int GetSafeWidth()
{
    try { return Math.Max(42, Console.WindowWidth - 1); }
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
