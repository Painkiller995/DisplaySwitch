# Testing

```bash
dotnet test                       # all suites
dotnet test tests/DisplaySwitch.Core.Tests
dotnet test tests/DisplaySwitch.App.Tests   # UI tests; screenshots in bin/<config>/net10.0/screenshots
```

**Simulated and real results are kept separate.** Automated tests use simulated monitors (`tests/DisplaySwitch.TestSupport/FakeDdcMonitor.cs`) and never touch hardware. Real-hardware results are recorded by hand below and in [HARDWARE.md](HARDWARE.md).

## Automated suites (122 tests, simulated hardware)

### `DisplaySwitch.Core.Tests` (83)

| Area | Covered |
|---|---|
| Input codes | Hex/decimal parsing, the reserved value 0, high-byte masking, catalog names |
| Capabilities parser | A real Samsung string, codes without separators, missing parentheses, truncation, empty and junk input |
| DDC/CI codec | Request frames against the specification's reference frame, reply parsing, bad checksum, unsupported feature, null message, wrong feature, capabilities fragments |
| EDID | Manufacturer, product and name; corrupt checksum; short data |
| Identity | PnP decoding, stable keys for identical monitors |
| Switching | Verified, read alias, already active, rejected code, ignored command (Samsung behaviour), silent after switch, rejected write, transient-error retries, **rapid repeated requests** (last wins, never concurrent), preview with automatic return, retired monitor |
| Channel | **Timeouts** without overlapping bus access, the reserved code is never written, garbled zero readings |
| Discovery | Capabilities caching, silent monitors, built-in profile overriding bad capabilities, the live input always listed |
| Picture adjustments | Loading, unsupported and unadvertised features, Kelvin conversion, range clamping, read-back verification, ignored writes, **rapid slider changes** (latest wins, never concurrent), failure recovery |
| Settings | Defaults, round trip, **corrupted file** (backed up and reported), invalid entries dropped, change notification, input layout and visibility |
| Shortcuts | Parse and format, rejection of unsafe gestures, conflict detection, reassignment, defaults |

### `DisplaySwitch.App.Tests` (39)

Headless Avalonia with real Skia rendering. The production `AppShell`, view models, views and settings run around simulated hardware.

| Flow | Covered |
|---|---|
| Home | Monitor, inputs and live input; light and dark themes; click to switch with verified feedback; honest "stayed on…" warning; **number keys** |
| Onboarding | All three steps, names, icons and default shortcuts saved; skip writes nothing else; the primary button label matches its accessible name |
| Editor | Rename, icon, shortcut; conflict warning; shortcut owned by another app; hide input; add input with validation and preview |
| Display controls | Brightness and color temperature shown when supported, slider writes and verifies, **arrow keys** adjust the focused slider, ignored and failed changes snap back with an explanation, Kelvin label, hidden when unsupported |
| Settings and states | Theme switching and persistence; reduce motion; **high contrast**; empty state; monitor without DDC/CI; discovery errors; multiple monitors and remembered selection; corrupted settings notice; global shortcut press; quick switcher |
| Layout | Minimum window size (560 × 520) and **150 % scaling** |

Screenshots of every state are saved for visual review and uploaded by CI.

## Quality gates (CI on Windows, Linux and macOS)

- `dotnet format --verify-no-changes`
- Build with `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, code style enforced, nullable reference types
- All tests

## Manual verification log

Performed on the development machine on 2026-11-05: Windows 11 Home (build 26300), Samsung C49J89x over DisplayPort.

| Check | Result |
|---|---|
| Monitor detection: name, EDID ID, connection, resolution | ✅ Pass |
| Live input detection (DisplayPort through read alias `0x03`) | ✅ Pass |
| Input switching via `0x60` on this monitor over DisplayPort | ❌ The monitor ignores the command (see HARDWARE.md); the app correctly reports *not confirmed* |
| Global shortcut Ctrl+Alt+1 through Windows `RegisterHotKey` | ✅ Pass (resolves to DisplayPort, already active, nothing written; repeated 3 times) |
| Onboarding end to end, driven through Windows UI Automation | ✅ Pass |
| Screen-reader names for every control (UI Automation tree) | ✅ Pass, after fixing one mismatched button name |
| Release build (trimmed, ReadyToRun): startup, rendering | ✅ Window visible in about 1.2 s (cold) |
| Idle cost | ✅ ~0.5 % of one core with the window open; **0 ms CPU** in the tray; ~114 MB private memory |
| Installer: silent per-user install, Start-menu entry, startup task, Apps & Features entry | ✅ Pass |
| In-app "Start when I sign in" reflects the installer's choice | ✅ Pass |
| `--quit` stops a running instance | ✅ Pass |
| Uninstall while running: process stopped, files, shortcut, startup entry and uninstall entry removed; user settings kept | ✅ Pass |

Open item: in an early manual session, one write of `0x11` was logged at the moment Ctrl+Alt+1 (bound to DisplayPort) was pressed by an automation script. It did not recur in three later reproductions with full logging. Shortcut presses and their resolved bindings are now always logged, so any recurrence is traceable.

### Not verified

- **macOS and Linux:** builds and packages are produced for every architecture, but DDC/CI, tray and autostart have **not been run on real macOS or Linux machines**.
- **A monitor that actually changes input via DDC/CI:** switching logic is verified against simulated monitors only. On the available monitor the command was accepted but ignored.
- Multiple physical monitors, 150 % scaling and high contrast on a real display (all covered by headless tests only).
