using System.Diagnostics;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using Vimon.Core;

namespace Vimon.Platform.Windows;

public sealed class WindowsTelemetry : IPlatformTelemetry
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsStorageEnabled = true
    };

    public string PlatformName => "Windows";

    public WindowsTelemetry() => _computer.Open();

    public SystemSnapshot Read()
    {
        foreach (var hw in _computer.Hardware) Update(hw);
        var all = Flatten(_computer.Hardware).ToList();

        var cpuHw = all.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        var cpu = cpuHw is null ? null : new CpuMetrics(
            cpuHw.Name,
            Find(cpuHw, SensorType.Temperature, "CPU Package", "CPU (Tctl/Tdie)", "Core (Tctl/Tdie)"),
            Find(cpuHw, SensorType.Load, "CPU Total", "Total"),
            Find(cpuHw, SensorType.Power, "CPU Package", "Package"),
            Find(cpuHw, SensorType.Clock, "CPU Core #1", "Core #1")
        );

        var pcieRows = QueryNvidiaPcie();
        var gpuHws = all.Where(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel).ToList();
        var gpus = new List<GpuMetrics>();

        for (int i = 0; i < gpuHws.Count; i++)
        {
            var h = gpuHws[i];
            var vendor = h.HardwareType switch
            {
                HardwareType.GpuNvidia => Vendor.Nvidia,
                HardwareType.GpuAmd => Vendor.Amd,
                HardwareType.GpuIntel => Vendor.Intel,
                _ => Vendor.Unknown
            };

            var pcie = vendor == Vendor.Nvidia && i < pcieRows.Count ? pcieRows[i] : default;
            gpus.Add(new GpuMetrics(
                i, h.Name, vendor,
                Find(h, SensorType.Temperature, "GPU Core", "GPU Temperature", "Core"),
                Find(h, SensorType.Temperature, "GPU Hot Spot", "GPU Hotspot", "Hot Spot", "Hotspot"),
                Find(h, SensorType.Temperature, "GPU Memory Junction", "GPU Memory", "Memory Junction", "Memory"),
                Find(h, SensorType.Load, "GPU Core", "GPU", "Core"),
                Find(h, SensorType.Power, "GPU Power", "GPU Package", "Board Power"),
                Find(h, SensorType.Control, "GPU Fan", "Fan") ?? Find(h, SensorType.Load, "GPU Fan", "Fan"),
                Find(h, SensorType.Clock, "GPU Core", "Core"),
                Find(h, SensorType.Clock, "GPU Memory", "Memory"),
                Find(h, SensorType.SmallData, "GPU Memory Used", "Memory Used") is double usedMb ? usedMb / 1024.0 : null,
                Find(h, SensorType.SmallData, "GPU Memory Total", "Memory Total") is double totalMb ? totalMb / 1024.0 : null,
                pcie.genCur, pcie.widthCur, pcie.genMax, pcie.widthMax
            ));
        }

        var mem = GetMemoryInfo();
        var storage = all.Where(h => h.HardwareType == HardwareType.Storage)
            .Select(h => new StorageMetrics(h.Name,
                h.Sensors.Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                    .Select(s => (double?)s.Value!.Value).DefaultIfEmpty(null).Max()))
            .ToList();

        return new(DateTimeOffset.Now, cpu, gpus, mem, storage);
    }

    private static double? Find(IHardware h, SensorType type, params string[] names)
    {
        var sensors = h.Sensors.Where(s => s.SensorType == type && s.Value.HasValue).ToList();
        foreach (var n in names)
        {
            var hit = sensors.FirstOrDefault(s => s.Name.Contains(n, StringComparison.OrdinalIgnoreCase));
            if (hit?.Value != null) return hit.Value.Value;
        }
        return sensors.FirstOrDefault()?.Value;
    }

    private static void Update(IHardware h)
    {
        h.Update();
        foreach (var sub in h.SubHardware) Update(sub);
    }

    private static IEnumerable<IHardware> Flatten(IEnumerable<IHardware> roots)
    {
        foreach (var h in roots)
        {
            yield return h;
            foreach (var x in Flatten(h.SubHardware)) yield return x;
        }
    }

    private static List<(int? genCur,int? widthCur,int? genMax,int? widthMax)> QueryNvidiaPcie()
    {
        var rows = new List<(int?,int?,int?,int?)>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=pcie.link.gen.current,pcie.link.width.current,pcie.link.gen.max,pcie.link.width.max --format=csv,noheader,nounits",
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return rows;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(1200);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var x = line.Split(',').Select(v => v.Trim()).ToArray();
                rows.Add(x.Length >= 4 ? (Parse(x[0]),Parse(x[1]),Parse(x[2]),Parse(x[3])) : default);
            }
        } catch { }
        return rows;
    }

    private static int? Parse(string s) => int.TryParse(s, out var n) ? n : null;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile;
        public ulong ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    private static MemoryMetrics GetMemoryInfo()
    {
        var m = new MEMORYSTATUSEX();
        GlobalMemoryStatusEx(m);
        const double gb = 1024d*1024d*1024d;
        double total = m.ullTotalPhys/gb, avail = m.ullAvailPhys/gb;
        double ct = m.ullTotalPageFile/gb, ca = m.ullAvailPageFile/gb;
        return new(total-avail, total, m.dwMemoryLoad, ct-ca, ct);
    }

    public void Dispose() => _computer.Close();
}
