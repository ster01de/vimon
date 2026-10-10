<div align="center">

# Vimon

**Minimal system telemetry. No sensor spam.**

A focused terminal monitor for the hardware metrics that actually matter.

`CPU · GPU · VRAM · RAM · PCIe · Power · Thermals`

![Vimon demo](docs/assets/vimon-hero.gif)

[![status](https://img.shields.io/badge/status-alpha-7CFF6B)](#roadmap)
[![platform](https://img.shields.io/badge/platform-Windows%20alpha%20%7C%20Linux%20planned%20%7C%20macOS%20planned-lightgrey)](#platforms)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8-purple)](https://dotnet.microsoft.com/)

</div>

---

## Why Vimon?

Most hardware monitors expose **everything**.

Vimon does the opposite: it surfaces the handful of metrics that help you decide whether the machine is healthy.

- Is the CPU too hot?
- Is the GPU thermally healthy?
- Is GPU memory getting uncomfortable?
- Is RAM pressure becoming a problem?
- Is the GPU actually using the expected PCIe link?
- Is power sensible for the workload?
- Is anything worth investigating now?

No 300-row sensor table. No hunting through labels.

---

## Current alpha

Windows is the first real backend.

Vimon currently supports:
- CPU temp / load / package power
- NVIDIA / AMD / Intel GPU discovery through LibreHardwareMonitor
- multi-GPU enumeration
- GPU core / hotspot / memory temperature when exposed
- GPU load / power / fan / clocks when exposed
- NVIDIA PCIe generation + lane width through `nvidia-smi`
- RAM usage + commit
- SSD temperature
- compact diagnostics
- GPU switching with arrow keys

Missing sensors are shown as `—`, not `0` and not `ERROR`.

---

## Multi-GPU

Vimon is **not** tied to RTX 3090.

```text
GPU 1/3  NVIDIA GeForce RTX 3090
GPU 2/3  NVIDIA GeForce RTX 3060
GPU 3/3  Intel Graphics
```

Use `←` / `→` to switch the focused GPU.

Future CLI:
```bash
vimon --gpu 0
vimon --gpu 1
vimon --gpu all
```

---

## Philosophy

Vimon is not trying to replace HWiNFO, lm-sensors, iStat Menus or vendor tools.

Those tools expose data.

Vimon tries to expose **meaning**.

```text
VRAM      86°C   OK
HOTSPOT   85°C   OK
GPU       75°C   OK
PCIE      GEN3 x8 / max GEN4 x16

Assessment:
Thermals are healthy.
PCIe width is below the adapter maximum.
Impact is workload-dependent.
```

---

## Architecture

```text
Vimon
├── Vimon.Core
│   ├── sensor models
│   ├── diagnostics
│   └── profiles
├── Vimon.Cli
│   └── terminal UI
└── platform backends
    ├── Windows      alpha
    ├── Linux        planned
    └── macOS        planned
```

GPU data uses a vendor-neutral model. Unsupported metrics are nullable by design.

---

## Profiles

Included:
- `default`
- `silent`
- `ai-workstation`

Thresholds are intentionally conservative in alpha. Vendor/model-specific overrides are planned.

---

## Build

Requires the .NET 8 SDK.

```powershell
.\build.ps1
```

Single-file Windows output:

```text
src\Vimon.Cli\bin\Release\net8.0-windows\win-x64\publish\vimon.exe
```

Run from source:

```powershell
.\run.ps1
```

---

## Controls

```text
← / →   switch GPU
P        pause
Space    pause
Q / Esc  quit
```

---

## Platforms

| Platform | Status | Backend |
|---|---|---|
| Windows | 🟢 Alpha | LibreHardwareMonitor + vendor tools |
| Linux | 🟡 Planned | `/sys/class/hwmon`, `/proc`, DRM/sysfs, vendor tools |
| macOS | 🟡 Planned | Apple Silicon telemetry, memory pressure, thermal state |

---

## Roadmap

### v0.1
- [x] terminal dashboard
- [x] vendor-neutral GPU model
- [x] multi-GPU enumeration
- [x] Windows telemetry backend
- [x] PCIe status for NVIDIA
- [x] RAM / storage overview
- [x] basic diagnostics
- [ ] hardened sensor mapping
- [ ] JSON config loading
- [ ] packaged release artifact

### v0.2
- [ ] `--compact`
- [ ] `--json`
- [ ] `--log`
- [ ] `--gpu`
- [ ] profile loading
- [ ] vendor/model thermal profiles
- [ ] CSV/JSON logging
- [ ] richer diagnostics

### v0.3
- [ ] Linux backend
- [ ] NVIDIA / AMD / Intel Linux GPU support
- [ ] packaged Linux releases

### v0.4
- [ ] macOS backend
- [ ] Apple Silicon CPU/GPU telemetry
- [ ] unified-memory pressure
- [ ] package power
- [ ] thermal state

### v1.0
- [ ] Windows / Linux / macOS
- [ ] stable sensor abstraction
- [ ] documented configuration
- [ ] CI release builds
- [ ] optional compact desktop widget

---

## Product rule

Before adding another sensor, ask:

> Does this metric help the user make a decision?

If not, it probably doesn't belong in the default Vimon view.

---

## License

MIT — see [LICENSE](LICENSE).

<div align="center">

**Less telemetry. More signal.**

</div>
