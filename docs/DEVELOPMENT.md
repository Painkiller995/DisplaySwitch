# Development

## Prerequisites

- **.NET 10 SDK** (`global.json` pins the feature band). On Windows: `winget install Microsoft.DotNet.SDK.10`
- Optional: **Inno Setup 6** to build the Windows installer (`winget install JRSoftware.InnoSetup`)

No IDE-specific tooling is required. Any editor with C# support works; Avalonia's XAML previewer is optional.

## Repository layout

```
src/
  DisplaySwitch.Core/        domain and application logic (no UI, no OS calls)
  DisplaySwitch.Platform/    Windows / Linux / macOS adapters
  DisplaySwitch.App/         Avalonia UI, design system, composition root
tests/
  DisplaySwitch.Core.Tests/  unit and behaviour tests
  DisplaySwitch.App.Tests/   headless UI tests with screenshots
  DisplaySwitch.TestSupport/ simulated monitors shared by both suites
tools/
  DisplaySwitch.IconGenerator/  renders every icon from the logo geometry
packaging/                   installers and scripts per platform
docs/                        architecture, design, hardware, testing, releasing
```

## Everyday commands

```bash
dotnet build                                  # warnings are errors
dotnet test
dotnet format DisplaySwitch.slnx              # apply formatting (CI runs --verify-no-changes)
dotnet run --project src/DisplaySwitch.App    # run the app
```

## Conventions

- **Build quality:** nullable reference types, `AnalysisLevel=latest-recommended`, code style enforced in the build, `TreatWarningsAsErrors`. Don't suppress a warning without a written reason next to the suppression.
- **Style:** `.editorconfig` is authoritative. File-scoped namespaces, `_camelCase` private fields, PascalCase constants and static readonly fields, braces always.
- **Logging:** use source-generated `[LoggerMessage]` methods. Log technical facts (monitor IDs, DDC results), never personal data.
- **Interop:** `LibraryImport` with blittable structs only; the Platform project disables runtime marshalling.
- **UI:** compiled bindings with `x:DataType` everywhere. Colours and sizes come from `Design/Tokens.axaml`; views pick a style class. Any spinner must bind its own `IsVisible`, because a hidden animated control keeps the render loop busy.
- **Comments** explain *why*. Public APIs and hardware interactions get XML docs.
- **Async:** no `.Result` or `.Wait()` on the UI thread. View-model state changes on the UI thread only.

## Debugging DDC/CI

- Logs: `%APPDATA%\DisplaySwitch\logs` (Windows), `~/Library/Application Support/DisplaySwitch/logs` (macOS), `~/.config/displayswitch/logs` (Linux). Settings › About › **Logs** opens the folder.
- The raw capabilities string is under Settings › Monitors › **Technical details**.
- DDC failures are logged with their Win32 or errno codes; retries are logged at Debug level.
- To test a monitor's input codes safely, use **Add input › Test this code**. It always returns to the current input.

## Icons

All icons are generated from the logo path (the same geometry as `Icon.Logo` in `Design/Icons.axaml`):

```bash
dotnet run --project tools/DisplaySwitch.IconGenerator
```

This writes the app icons (`.ico`, tray PNGs) to `src/DisplaySwitch.App/Assets` and the packaging icons (`.icns`, large PNGs) to `packaging/icons`.

## README screenshots

The images in `docs/screenshots` are rendered by `tests/DisplaySwitch.App.Tests/ReadmeScreenshotTests.cs` from the real UI around a simulated monitor, so they never show anyone's desktop. After a visual change, regenerate them:

```bash
DISPLAYSWITCH_UPDATE_SCREENSHOTS=1 dotnet test --filter ReadmeScreenshotTests      # bash
$env:DISPLAYSWITCH_UPDATE_SCREENSHOTS=1; dotnet test --filter ReadmeScreenshotTests # PowerShell
```

Without the variable the tests still render every screen, so a broken view fails the suite, but they leave the repository untouched.

## Settings file

`settings.json` (in the folder above the logs) is human-readable and safe to edit while the app is closed. Invalid entries are dropped on load; an unreadable file is backed up as `settings.json.corrupt-<timestamp>` and the user is told.
