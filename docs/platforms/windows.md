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
.\vimon-windows-x64.exe --compact         # one- or two-line view (toggle with C)
.\vimon-windows-x64.exe --widget          # borderless list for a desktop panel
.\vimon-windows-x64.exe --theme mono      # no colours; also chosen automatically when NO_COLOR is set
```

All options and keys: [README → Controls](../../README.md#controls).

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
| Disk throughput | LibreHardwareMonitor storage sensors *Read Rate* and *Write Rate* |
| Free disk space | .NET `DriveInfo` (fixed and removable drives), read every 5 s |
| Network throughput | `NetworkInterface.GetIPStatistics` (64-bit counters). Left out: loopback, tunnels and virtual adapters (Hyper-V, VMware, VirtualBox, WireGuard, TAP, Bluetooth) |

Which GPU values appear depends on what the GPU and driver expose through LibreHardwareMonitor. Missing values are shown as `—`.

## Not available (yet)

| Value | Why |
|---|---|
| Busiest processes | Planned (`NtQuerySystemInformation`, which also sees protected processes without admin rights). The PROCESSES panel does not appear on Windows until then. |
| OS memory pressure, thermal state | Windows has no direct equivalent of the macOS signals. |

## Multi-GPU

All NVIDIA, AMD and Intel GPUs are listed; switch with `←` / `→`. PCIe data is matched by model name, so mixed setups such as RTX 3090 + RTX 3060 get the right values. Several identical cards are matched in `nvidia-smi` order.

## Troubleshooting

| Symptom | Fix |
|---|---|
| CPU temperature `—` | Run as administrator. |
| Symbols show as `?` | The classic console window (no tabs, e.g. after *Run as administrator*) uses a font without them. vimon detects it and switches to ASCII symbols; for the full look use *Windows Terminal (Admin)* (Win+X) or set the console font to *Cascadia Mono*. `--ascii` forces the plain symbols anywhere. |
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
