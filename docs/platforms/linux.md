# Vimon on Linux

[← Back to README](../../README.md) · [Windows](windows.md) · [macOS](macos.md)

**Status:** planned. There is no Linux backend and no Linux release yet.

A source build already runs on Linux, but exits right away with:

```text
vimon: The Linux backend is not implemented yet (planned: /sys/class/hwmon, /proc, nvidia-smi and DRM/sysfs).
```

## Planned data sources

Linux exposes most of what Vimon needs as plain files, no extra daemons required.

| Value | Planned source |
|---|---|
| CPU load | `/proc/stat` |
| CPU temperature | `/sys/class/hwmon` (`coretemp`, `k10temp`) |
| RAM used | `/proc/meminfo` |
| Memory pressure | `/proc/pressure/memory` (PSI) |
| NVIDIA GPUs | `nvidia-smi` (the parser already lives in `Vimon.Core`) |
| AMD GPU load and VRAM | `/sys/class/drm/card*/device/` |
| GPU and NVMe temperatures | `/sys/class/hwmon` (`amdgpu`, `nvme`) |
| Network throughput | `/proc/net/dev` |
| Busiest processes | `/proc/<pid>/stat` and `/proc/<pid>/status` |

Releases will follow the macOS pattern: self-contained `linux-x64` and `linux-arm64` tarballs built and smoke-tested in CI.

## Help wanted

The backend lives in `src/Vimon.Platform.Linux`. The macOS backend in `src/Vimon.Platform.MacOS` shows the shape: small readers per area, pure parsing logic with unit tests, and integration tests that run on the real OS in CI. See [CONTRIBUTING](../../CONTRIBUTING.md).
