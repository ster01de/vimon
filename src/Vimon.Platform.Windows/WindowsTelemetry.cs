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
        foreach (var hw in _computer.Hardware)
            Update(hw);

        var all = Flatten(_computer.Hardware).ToList();

        var cpuHw = all.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

        var cpu = cpuHw is null ? null : new CpuMetrics(
            cpuHw.Name,
            FindNamed(cpuHw, SensorType.Temperature, true,
                "CPU (Tctl/Tdie)", "Core (Tctl/Tdie)", "CPU Package", "Package"),
            FindNamed(cpuHw, SensorType.Load, false,
                "CPU Total", "Total"),
            FindNamed(cpuHw, SensorType.Power, true,
                "CPU Package", "Package", "CPU PPT", "PPT"),
            FindNamed(cpuHw, SensorType.Clock, true,
                "CPU Core #1", "Core #1")
        );

        var nvidiaPcie = QueryNvidiaPcie();
        int nvidiaIndex = 0;

        var gpuHws = all.Where(h =>
            h.HardwareType is HardwareType.GpuNvidia
            or HardwareType.GpuAmd
            or HardwareType.GpuIntel).ToList();

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

            (int? genCur, int? widthCur, int? genMax, int? widthMax) pcie = default;

            if (vendor == Vendor.Nvidia)
            {
                if (nvidiaIndex < nvidiaPcie.Count)
                    pcie = nvidiaPcie[nvidiaIndex];

                nvidiaIndex++;
            }

            gpus.Add(new GpuMetrics(
                i,
                h.Name,
                vendor,
                FindNamed(h, SensorType.Temperature, true,
                    "GPU Core", "GPU Temperature"),
                FindNamed(h, SensorType.Temperature, true,
                    "GPU Hot Spot", "GPU Hotspot", "Hot Spot", "Hotspot"),
                FindNamed(h, SensorType.Temperature, true,
                    "GPU Memory Junction", "Memory Junction", "GPU Memory"),
                FindNamed(h, SensorType.Load, false,
                    "GPU Core", "GPU D3D", "GPU"),
                FindNamed(h, SensorType.Power, true,
                    "GPU Power", "GPU Package", "Board Power"),
                FindNamed(h, SensorType.Control, false,
                    "GPU Fan", "Fan")
                    ?? FindNamed(h, SensorType.Load, false,
                        "GPU Fan", "Fan"),
                FindNamed(h, SensorType.Clock, true,
                    "GPU Core", "Core"),
                FindNamed(h, SensorType.Clock, true,
                    "GPU Memory", "Memory"),
                FindNamed(h, SensorType.SmallData, false,
                    "GPU Memory Used", "Memory Used") is double usedMb
                        ? usedMb / 1024.0
                        : null,
                FindNamed(h, SensorType.SmallData, true,
                    "GPU Memory Total", "Memory Total") is double totalMb
                        ? totalMb / 1024.0
                        : null,
                pcie.genCur,
                pcie.widthCur,
                pcie.genMax,
                pcie.widthMax
            ));
        }

        var memory = GetMemoryInfo();

        var storage = all
            .Where(h => h.HardwareType == HardwareType.Storage)
            .Select(h => new StorageMetrics(
                h.Name,
                FindAnyPositive(h, SensorType.Temperature)))
            .ToList();

        return new SystemSnapshot(
            DateTimeOffset.Now,
            cpu,
            gpus,
            memory,
            storage
        );
    }

    private static double? FindNamed(
        IHardware hardware,
        SensorType type,
        bool positiveOnly,
        params string[] names)
    {
        var sensors = hardware.Sensors
            .Where(s => s.SensorType == type && s.Value.HasValue)
            .ToList();

        foreach (var name in names)
        {
            var hit = sensors.FirstOrDefault(s =>
                s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            hit ??= sensors.FirstOrDefault(s =>
                s.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

            if (hit?.Value is float value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    continue;

                if (positiveOnly && value <= 0)
                    continue;

                return value;
            }
        }

        return null;
    }

    private static double? FindAnyPositive(IHardware hardware, SensorType type)
    {
        foreach (var sensor in hardware.Sensors
                     .Where(s => s.SensorType == type && s.Value.HasValue))
        {
            float value = sensor.Value!.Value;

            if (!float.IsNaN(value) && !float.IsInfinity(value) && value > 0)
                return value;
        }

        return null;
    }

    private static void Update(IHardware hardware)
    {
        hardware.Update();

        foreach (var sub in hardware.SubHardware)
            Update(sub);
    }

    private static IEnumerable<IHardware> Flatten(IEnumerable<IHardware> roots)
    {
        foreach (var hardware in roots)
        {
            yield return hardware;

            foreach (var sub in Flatten(hardware.SubHardware))
                yield return sub;
        }
    }

    private static List<(int? genCur, int? widthCur, int? genMax, int? widthMax)> QueryNvidiaPcie()
    {
        var rows = new List<(int?, int?, int?, int?)>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=pcie.link.gen.current,pcie.link.width.current,pcie.link.gen.max,pcie.link.width.max --format=csv,noheader,nounits",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);

            if (process is null)
                return rows;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1200);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var values = line.Split(',').Select(value => value.Trim()).ToArray();

                rows.Add(values.Length >= 4
                    ? (Parse(values[0]), Parse(values[1]), Parse(values[2]), Parse(values[3]))
                    : default);
            }
        }
        catch
        {
            // NVIDIA tooling is optional.
        }

        return rows;
    }

    private static int? Parse(string value) =>
        int.TryParse(value, out int number) ? number : null;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    private static MemoryMetrics GetMemoryInfo()
    {
        var memory = new MEMORYSTATUSEX();
        GlobalMemoryStatusEx(memory);

        const double gb = 1024d * 1024d * 1024d;

        double total = memory.ullTotalPhys / gb;
        double available = memory.ullAvailPhys / gb;
        double commitTotal = memory.ullTotalPageFile / gb;
        double commitAvailable = memory.ullAvailPageFile / gb;

        return new MemoryMetrics(
            total - available,
            total,
            memory.dwMemoryLoad,
            commitTotal - commitAvailable,
            commitTotal
        );
    }

    public void Dispose() => _computer.Close();
}
