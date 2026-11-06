# Architecture

DisplaySwitch has three layers, each in its own project. Dependencies point inward only.

```
DisplaySwitch.App  (Avalonia UI: views, view models, tray, shortcuts, theming)
        │
        ├──► DisplaySwitch.Platform  (Windows / Linux / macOS adapters)
        │            │
        └────────────┴──► DisplaySwitch.Core  (domain + application logic, no UI, no OS calls)
```

| Project | Responsibility | Depends on |
|---|---|---|
| `DisplaySwitch.Core` | Input codes, MCCS catalog, capabilities parsing, DDC/CI packet codec, EDID parsing, switching workflow, settings model and persistence, shortcut rules. Defines the **ports** the platform implements. | Logging abstractions only |
| `DisplaySwitch.Platform` | Implements the ports for each OS: monitor enumeration and DDC/CI, global hotkeys, start at sign-in. | Core |
| `DisplaySwitch.App` | Presentation (MVVM with CommunityToolkit.Mvvm), design system, composition root. | Core, Platform |

## Ports (interfaces at real boundaries)

Interfaces exist only where an implementation genuinely varies by platform or must be replaced in tests:

| Port | Implementations |
|---|---|
| `IDisplayBackend`, `IDdcMonitor` | `WindowsDisplayBackend` (dxva2 + CCD), `LinuxDisplayBackend` (sysfs + i2c-dev), `MacDisplayBackend` (IOAVService), test fakes |
| `IGlobalHotkeyService` | `WindowsHotkeyService` (`RegisterHotKey` on a message-only thread), an "unsupported" service on macOS/Linux, test fake |
| `IAutostartService` | Registry Run key (Windows), XDG autostart file (Linux), LaunchAgent (macOS), test fake |
| `ISettingsStore` | `JsonSettingsStore` |
| `ICapabilitiesCache` | `SettingsCapabilitiesCache` (stores the slow-to-read capabilities string in settings) |

`PlatformServices.CreateForCurrentOS` is the single place that chooses implementations.

## Switching a monitor's input

```
Card click / shortcut / tray / number key
   → MonitorViewModel.SwitchToAsync            (UI state: spinner, banner)
   → MonitorController.SwitchAsync             (workflow)
        1. refuse codes that are not configured inputs   → Rejected
        2. read the current input; skip if already there → AlreadyActive
        3. write VCP 0x60                                → Failed on error
        4. poll the monitor until it confirms or time runs out:
             reports the target (or a read alias)        → Verified
             keeps reporting another input               → NotConfirmed
             stops answering                             → SentWithoutConfirmation
   → MonitorChannel                            (exclusive, retried, time-limited DDC access)
   → IDdcMonitor                               (blocking platform call)
```

Key decisions:

- **One gate per monitor (`MonitorChannel`).** DDC/CI is a slow, half-duplex bus, so requests never overlap. A native call that hangs cannot be aborted. On timeout the caller is released, but the gate stays held until the call returns.
- **Retries with exponential backoff.** Real hardware (a Samsung C49J89x) fails about one request in four, in bursts of about a second.
- **Latest request wins.** A newer switch on the same monitor supersedes one still verifying, so rapid key presses settle on the last.
- **Writes are limited to configured inputs.** The UI never writes arbitrary values. User-added codes are explicit, validated and testable with automatic return.
- **Read aliases.** Some monitors read back a different value from the one written. `InputSource.ReadAliases` captures that, so active-input detection still works.
- **Honest outcomes.** Only `Verified` and `AlreadyActive` count as success; `SwitchFeedback` turns every outcome into wording that claims nothing more.

## Picture adjustments

`PictureAdjustments` (one per `MonitorController`, sharing its `MonitorChannel`) handles brightness (VCP `0x10`) and color temperature (`0x0C`, shown in Kelvin when the monitor reports its step via `0x0B`).

- **Probing:** a feature is read only when the capabilities string advertises it, or when no capabilities are available. Loading runs after discovery, so it never delays the input list.
- **Writing:** `CoalescingVcpWriter` applies "latest value wins". Slider drags produce values faster than DDC/CI can apply them, so at most one write is in flight and intermediate values are dropped. Values are clamped to the range the monitor reports.
- **Verification:** the latest request reads the value back and returns `Applied`, `NotApplied` (accepted but ignored), `Failed` or `Superseded`. The UI snaps back and explains anything other than `Applied`.

## Input list resolution

`MonitorDirectory.DetectInputs` chooses the default inputs for a monitor:

1. a **built-in profile** (`MonitorProfiles`) for models whose capabilities string is wrong;
2. otherwise the inputs **advertised** in the capabilities string;
3. plus the **currently reported** input, so the live input always appears.

The user's customisation (`MonitorSettings.Inputs`) replaces the detected list once it exists. `null` means "use detection", so detection improvements reach users who never customised anything.

## State ownership

- `ISettingsStore` owns persisted state. `AppSettings` is immutable, and changes go through `Update(Func<AppSettings, AppSettings>)`. Saves are queued in the background and written atomically (write to `.tmp`, then rename). A corrupt file is moved aside and reported, never silently discarded.
- `MonitorDirectory` owns monitor handles and controllers, and disposes replaced ones off the UI thread.
- `WorkspaceViewModel` is the single UI-side source of truth for monitors and selection, shared by the main window, quick switcher, tray menu and shortcut service.
- The operating system is the source of truth for "start at sign-in"; the app reads it back instead of storing a copy.

## Threading

All view-model state lives on the UI thread. DDC/CI calls run on the thread pool inside `MonitorChannel` and return results through `await`. Windows hotkeys arrive on their own thread and are marshalled with `Dispatcher.UIThread.Post`.

## Windows interop details

- dxva2 exposes physical monitors only as "Generic PnP Monitor", so `QueryDisplayConfig` / `DisplayConfigGetDeviceInfo` (CCD) supply the model name, EDID IDs and connector type. These are matched by GDI device name (`\\.\DISPLAYn`).
- All interop uses `LibraryImport` source generation with blittable structs (`InlineArray` buffers) and `DisableRuntimeMarshalling`, which is trim- and AOT-safe.
- Physical monitor handles are owned by `SafePhysicalMonitorHandle`.

## Composition and lifetime

`App` builds `AppServices` (settings, logging, platform, directory) and `AppShell`, which owns the windows, the tray and the view models. UI tests build the same `AppShell` around simulated hardware, so everything above the platform layer runs as it does in production. Closing the main window destroys it (releasing rendering resources) while the app stays in the tray. A second launch shows the window, and `DisplaySwitch --quit` exits gracefully (used by the installer). Both requests travel over a per-user named pipe.
