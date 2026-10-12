# Vimon on Windows

[← Back to README](../../README.md) · [macOS](macos.md) · [Linux](linux.md)

**Status:** alpha · x64 · Windows 10 and 11

## Install

1. Download `vimon-windows-x64.exe` from the [releases](https://github.com/ster01de/vimon/releases).
2. Open Windows Terminal or PowerShell **as administrator** and run it:

```powershell
.\vimon-windows-x64.exe
.\vimon-windows-x64.exe --once            # one frame, then exit
.\vimon-windows-x64.exe --hide-missing    # start with empty rows hidden
```

The executable is self-contained (no .NET install needed) and not code-signed yet. SmartScreen may show *Windows protected your PC* on first start: choose *More info → Run anyway*.

## Why administrator?

LibreHardwareMonitor needs driver access for many sensors. Without admin rights Vimon still runs, but CPU temperatures and some motherboard and storage sensors stay `—`.

## Data sources

| Area | Source |
|---|---|
| CPU temperature, load, package power | [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) 0.9.4 |
| GPU temperatures, load, memory, power, fan, clocks | LibreHardwareMonitor (NVIDIA, AMD, Intel) |
| PCIe generation and lane width | `nvidia-smi` (NVIDIA only), matched to GPUs by model name |
| RAM | `GlobalMemoryStatusEx` |
| Storage temperature | LibreHardwareMonitor |
| Network throughput | `NetworkInterface.GetIPStatistics` (64-bit counters). Left out: loopback, tunnels and virtual adapters (Hyper-V, VMware, VirtualBox, WireGuard, TAP, Bluetooth) |

Which GPU values appear depends on what the GPU and driver expose through LibreHardwareMonitor. Missing values are shown as `—`.

## Multi-GPU

All NVIDIA, AMD and Intel GPUs are listed; switch with `←` / `→`. PCIe data is matched by model name, so mixed setups such as RTX 3090 + RTX 3060 get the right values. Several identical cards are matched in `nvidia-smi` order.

## Troubleshooting

| Symptom | Fix |
|---|---|
| CPU temperature `—` | Run as administrator. |
| PCIe `—` on an NVIDIA GPU | Make sure `nvidia-smi` is on `PATH` (it ships with the NVIDIA driver). |
| PCIe `—` on AMD / Intel | Expected: PCIe data currently comes from `nvidia-smi` only. |
| Frame cut off with *… more lines* | Make the terminal window taller. |

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
.\run.ps1      # run from source
.\build.ps1    # single-file vimon.exe in src\Vimon.Cli\bin\Release\net8.0-windows\win-x64\publish\
dotnet test
```
