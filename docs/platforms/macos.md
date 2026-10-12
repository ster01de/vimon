# Vimon on macOS

[← Back to README](../../README.md) · [Windows](windows.md) · [Linux](linux.md)

**Status:** alpha · Apple Silicon and Intel · tested on an Apple M2 with macOS 13

## Install

```bash
curl -fLO https://github.com/ster01de/vimon/releases/download/v0.5.0-alpha/vimon-macos-arm64.tar.gz
tar -xzf vimon-macos-arm64.tar.gz
./vimon
```

For a newer version, replace `v0.5.0-alpha` with the tag from the [releases page](https://github.com/ster01de/vimon/releases).

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
vimon --widget          # borderless list for a desktop panel (see below)
```

The live view runs in the terminal's alternate screen, like `top`: resizing does not leave copies in the scrollback, and your previous terminal content returns when you quit with `Q`, `Esc` or `Ctrl+C`. Give the window at least ~40 rows to see everything; shorter windows are cut from the bottom, STATUS stays on top.

## Desktop widget with kitty

[kitty](https://sw.kovidgoyal.net/kitty/) 0.42 or newer can show a terminal program as a borderless panel on the desktop. `vimon --widget` is made for it: no borders, no header, no background colours, so kitty's transparency shows through.

```bash
brew install --cask kitty
/Applications/kitty.app/Contents/MacOS/kitten panel --detach \
  --edge=none --columns=31 --lines=9 \
  --margin-left=20 --margin-top=40 \
  --layer=bottom -o background_opacity=0.25 \
  /usr/local/bin/vimon --widget
```

- `--edge=none` anchors the panel at the top-left corner; move it with `--margin-left` and `--margin-top` (pixels).
- `--layer=bottom` keeps it behind your windows like a desktop widget; `--layer=top` keeps it above them.
- `background_opacity` goes from `0` (fully transparent) to `1`.
- `--lines=9` fits status, two findings, CPU, GPU, RAM, network, pressure and thermal state.

The panel takes no keyboard input. Stop it with `pkill -f "vimon --widget"`. Starting it automatically at login is not built in yet.

All panel options: `kitten panel --help`.

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
| Network throughput | Interface byte counters of the routing table (`sysctl NET_RT_IFLIST2`). macOS gives apps only the low 32 bits, rounded to 1 KB; vimon bridges the wrap every 4 GB. Left out: loopback, VPN tunnels (`utun`), AirDrop and peer-to-peer Wi-Fi (`awdl`, `llw`), Personal Hotspot (`ap`), internal bridges (`anpi`, `bridge`) |

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
| Widget cut off in the kitty panel | Raise `--lines` or `--columns`. |

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet run --project src/Vimon.Cli -f net8.0
dotnet test
```
