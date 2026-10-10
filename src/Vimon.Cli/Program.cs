using Vimon.Core;
using Vimon.Platform.Windows;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.CursorVisible = false;

int selectedGpu = 0;
bool running = true;
bool paused = false;
int frame = 0;
var spinner = new[] { "⠋","⠙","⠹","⠸","⠼","⠴","⠦","⠧","⠇","⠏" };

using IPlatformTelemetry telemetry = new WindowsTelemetry();

while (running)
{
    if (!paused)
    {
        var s = telemetry.Read();
        var gpu = s.Gpus.Count > 0 ? s.Gpus[Math.Clamp(selectedGpu,0,s.Gpus.Count-1)] : null;
        var d = DiagnosticsEngine.Evaluate(s);

        Console.SetCursorPosition(0,0);
        int w = Math.Max(80, Math.Min(Console.WindowWidth-1, 120));
        Line(w,'┌','─','┐');
        Row(w, $" VIMON  {spinner[frame++ % spinner.Length]} LIVE", $"GPU {selectedGpu+1}/{Math.Max(1,s.Gpus.Count)}   [←/→ switch]   [P pause] [Q quit]");
        Line(w,'├','─','┤');

        var cpu = s.Cpu;
        Row(w,
            $" CPU  {Fmt(cpu?.TempC,"°C"),7}  {Fmt(cpu?.LoadPct,"%"),7}  {Fmt(cpu?.PackagePowerW,"W"),7}",
            gpu is null ? " GPU  n/a" : $" {gpu.Name}");

        Row(w,
            $" RAM  {s.Memory.UsedGb:0.0}/{s.Memory.TotalGb:0.0} GB  {s.Memory.LoadPct:0}%",
            gpu is null ? "" : $" CORE {Fmt(gpu.CoreTempC,"°C")}  HOT {Fmt(gpu.HotspotTempC,"°C")}  MEM {Fmt(gpu.MemoryTempC,"°C")}");

        Row(w,
            $" SSD  {(s.Storage.Count>0 ? Fmt(s.Storage.Max(x=>x.TempC),"°C") : "n/a")}",
            gpu is null ? "" : $" LOAD {Fmt(gpu.UtilizationPct,"%")}  PWR {Fmt(gpu.PowerW,"W")}  FAN {Fmt(gpu.FanPct,"%")}");

        Row(w,
            $" STATUS  {string.Join(" · ", d.Take(3).Select(x=>x.Code))}",
            gpu is null ? "" : Pcie(gpu));

        Line(w,'└','─','┘');
    }

    var end = DateTime.UtcNow.AddMilliseconds(500);
    while (DateTime.UtcNow < end)
    {
        if (Console.KeyAvailable)
        {
            var k = Console.ReadKey(true).Key;
            if (k is ConsoleKey.Q or ConsoleKey.Escape) running = false;
            else if (k is ConsoleKey.P or ConsoleKey.Spacebar) paused = !paused;
            else if (k == ConsoleKey.RightArrow) selectedGpu++;
            else if (k == ConsoleKey.LeftArrow) selectedGpu = Math.Max(0, selectedGpu-1);
        }
        Thread.Sleep(25);
    }
}

Console.CursorVisible = true;
Console.Clear();

static string Fmt(double? v, string u) => v.HasValue ? $"{v:0.#}{u}" : "—";
static string Pcie(GpuMetrics g)
{
    if (!g.PcieGenerationCurrent.HasValue) return "PCIE —";
    return $"PCIE GEN{g.PcieGenerationCurrent} x{g.PcieWidthCurrent} / max GEN{g.PcieGenerationMax} x{g.PcieWidthMax}";
}
static void Line(int w, char l, char m, char r) => Console.WriteLine(l + new string(m,w-2) + r);
static void Row(int w, string l, string r)
{
    int inner=w-2;
    int spaces=Math.Max(1,inner-l.Length-r.Length);
    var line=l+new string(' ',spaces)+r;
    if (line.Length>inner) line=line[..inner];
    Console.WriteLine("│"+line.PadRight(inner)+"│");
}
