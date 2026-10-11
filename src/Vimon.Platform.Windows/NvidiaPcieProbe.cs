using System.ComponentModel;
using System.Diagnostics;
using Vimon.Core;

namespace Vimon.Platform.Windows;

/// <summary>
/// Reads PCIe link state through nvidia-smi. Results are cached because spawning a process
/// for every frame is wasteful and link changes only matter at human time scales.
/// </summary>
internal sealed class NvidiaPcieProbe
{
    private const long CacheDurationMs = 5000;
    private const int TimeoutMs = 1500;

    private IReadOnlyList<NvidiaSmiGpu> _cached = [];
    private long? _cachedAtMs;
    private bool _isAvailable = true;

    public IReadOnlyList<NvidiaSmiGpu> Read()
    {
        if (!_isAvailable)
            return [];

        long now = Environment.TickCount64;

        if (_cachedAtMs is long cachedAt && now - cachedAt < CacheDurationMs)
            return _cached;

        _cached = Query();
        _cachedAtMs = now;
        return _cached;
    }

    private IReadOnlyList<NvidiaSmiGpu> Query()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "nvidia-smi",
            Arguments = NvidiaSmi.PcieQueryArguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Process? process;

        try
        {
            process = Process.Start(psi);
        }
        catch (Win32Exception)
        {
            // nvidia-smi is optional. When it is not on PATH, stop retrying for this session.
            _isAvailable = false;
            return [];
        }

        if (process is null)
            return [];

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            // Drain stderr so a full pipe cannot block nvidia-smi.
            _ = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(TimeoutMs))
            {
                TryKill(process);
                return [];
            }

            return process.ExitCode == 0
                ? NvidiaSmi.ParsePcieQuery(stdout.GetAwaiter().GetResult())
                : [];
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // The process exited between the timeout and the kill, or cannot be terminated.
            // Either way there is nothing left to do but drop this sample.
        }
    }
}
