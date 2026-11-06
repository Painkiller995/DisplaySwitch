# Changelog

## 1.0.1 — 2026-11-05

### Bug fixes

- Samsung C49J89x: use the input values its firmware advertises (`0x03` DisplayPort, `0x01` HDMI) instead of standard MCCS codes.

## 1.0.0 — 2026-11-05

First release.

### Features

- Detects monitors automatically and shows each one at its real shape, with its model name, connection type and resolution.
- Switches inputs with one click, from the tray or menu bar, with number keys 1–9, or with global shortcuts (Windows; defaults Ctrl+Alt+1, 2, 3…).
- Reports results honestly: "Now showing…" appears only after the monitor confirms the change. Otherwise DisplaySwitch says what it knows (the monitor stayed on its input, or stopped answering).
- Lets you name inputs, choose icons, assign or clear shortcuts (with conflict warnings), hide inputs, and add inputs by code with a safe 6-second test that returns automatically.
- Supports multiple monitors, each with its own inputs and shortcuts.
- Guides first-time users through a short setup and assigns default shortcuts.
- Includes light, dark and automatic high-contrast themes, reduced motion, full keyboard operation and screen-reader labels.
- Can start at sign-in, keep running in the tray when the window is closed, and show a quick switcher from the tray icon.
- Has no telemetry. Settings and logs stay on your computer, and a damaged settings file is backed up and reported.

### Platforms

- **Windows 10 1809+ / 11 (x64):** installer (per-user, no admin) and portable zip. Verified on real hardware.
- **macOS 12+ (Apple silicon, Intel build provided):** DMG. DDC/CI via IOAVService; not yet verified on real hardware. No global shortcuts yet.
- **Linux (x64, arm64):** tarball with install script. DDC/CI via i2c-dev; not yet verified on real hardware. No global shortcuts yet.

### Known issues

- **Samsung C49J89x:** over DisplayPort the monitor accepts but ignores input-switch commands, and DisplaySwitch reports this. Detecting the live input works. See `docs/HARDWARE.md`.
- Release packages are not code-signed unless built with a certificate. Windows SmartScreen and macOS Gatekeeper warn on first launch.
- Monitors on the proprietary NVIDIA driver for Linux are not detected.
