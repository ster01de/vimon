# Vimon on macOS

[← Back to README](../../README.md) · [Windows](windows.md) · [Linux](linux.md)

**Status:** alpha · Apple Silicon and Intel · tested on an Apple M2 with macOS 13

## Install

```bash
curl -fLO https://github.com/ster01de/vimon/releases/download/v0.4.0-alpha/vimon-macos-arm64.tar.gz
tar -xzf vimon-macos-arm64.tar.gz
./vimon
```

For a newer version, replace `v0.4.0-alpha` with the tag from the [releases page](https://github.com/ster01de/vimon/releases).

| Mac | File |
|---|---|
| Apple Silicon (M1, M2, M3, M4) | `vimon-macos-arm64.tar.gz` |
| Intel | `vimon-macos-x64.tar.gz` |

Not sure? `uname -m` prints `arm64` on Apple Silicon.

The binary is self-contained (no .NET install needed) and ad-hoc signed, but not notarized yet. If you downloaded it with a browser, macOS blocks the first start with *"vimon" cannot be opened*. Remove the quarantine flag once:

```bash
xattr -d com.apple.quarantine vimon
```

Optional, to run `vimon` from anywhere:

```bash
sudo mv vimon /usr/local/bin/
```

## Usage

```bash
vimon                   # live view, refreshes every 0.5 s
vimon --hide-missing    # start with empty rows hidden (toggle with H)
vimon --once            # one frame, then exit
vimon --compact         # one- or two-line view (toggle with C)
vimon --once --compact  # single status line, e.g. for tmux or SketchyBar
```

The live view runs in the terminal's alternate screen, like `top`: resizing does not leave copies in the scrollback, and your previous terminal content returns when you quit with `Q`, `Esc` or `Ctrl+C`. Give the window at least ~40 rows to see everything; shorter windows are cut from the bottom, STATUS stays on top.

## No root needed

Everything is read through public system interfaces plus read-only SMC access:

| Value | Source |
|---|---|
| CPU name | `sysctl machdep.cpu.brand_string` |
| CPU load | Mach host CPU tick counters |
| CPU temperature | SMC: average of the `Tp*` / `Te*` core sensors (Intel Macs: `TC*`) |
| GPU name, load, memory in use | IORegistry `IOAccelerator` performance statistics |
| GPU core / hotspot temperature | SMC: average / hottest of the `Tg*` sensors (Intel Macs: `TG*`) |
| RAM used | Mach VM statistics, counted like Activity Monitor (app + wired + compressed) |
| Memory pressure | `sysctl kern.memorystatus_vm_pressure_level` |
| Thermal state | `NSProcessInfo.thermalState`, the signal macOS uses for throttling |

## Not available (yet)

| Row | Why |
|---|---|
| CPU / GPU power | Needs the private IOReport API; planned. |
| Fan | MacBook Air models with Apple Silicon have no fan. Fan speed for other Macs is planned. |
| VRAM temperature, PCIe | Do not exist on Apple Silicon (unified memory, integrated GPU). |
| Storage temperature | Not implemented yet. |

Press `H` to hide these rows.

## Good to know

- **Memory pressure** is the better RAM signal on macOS than the usage percentage: macOS keeps memory busy on purpose. Compare with *Memory Pressure* in Activity Monitor.
- **CPU temperature warnings** start at 80 °C for every machine today. Fanless MacBook Airs can run above that under sustained load by design. Apple Silicon thresholds are planned.
- **Intel Macs** use different sensor keys. Support is built in but untested so far; reports are welcome.
- **Virtual machines** usually expose no SMC sensors, so temperatures show `—` there.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `tar: Error opening archive: Unrecognized archive format` | The download is GitHub's 9-byte "Not Found" page (check with `ls -l`). Fix the version in the URL and download again with `curl -fLO`. |
| *"vimon" cannot be opened* | `xattr -d com.apple.quarantine vimon` |
| Temperatures `—` | Expected in virtual machines without SMC sensors. |
| Frame cut off with *… more lines* | Make the terminal window taller. |

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet run --project src/Vimon.Cli -f net8.0
dotnet test
```
