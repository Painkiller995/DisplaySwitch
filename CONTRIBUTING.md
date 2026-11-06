# Contributing to DisplaySwitch

Thanks for helping! Bug reports, monitor compatibility reports, fixes and features are all welcome.

## The most valuable contribution: monitor reports

Input switching depends entirely on monitor firmware, and nobody owns every monitor. If you test DisplaySwitch on a monitor, please open a **Monitor compatibility report** issue (there's a template) with:

- the model and hardware ID (shown under *Settings › Monitors › Technical details*, or in the log);
- how your computer is connected (DisplayPort, HDMI, USB-C, through a dock…);
- which input codes worked, which were ignored, and what the monitor reports while on each input.

Use **Add input › Test this code** to try codes safely. The monitor returns to the current input automatically after 6 seconds.

Verified results go into [`docs/HARDWARE.md`](docs/HARDWARE.md) and, when a monitor misreports its inputs, into a built-in profile.

## Development setup

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). Nothing else is required to build and test.

```bash
dotnet build      # analyzers on; warnings are errors
dotnet test       # unit tests and headless UI tests, no hardware needed
dotnet run --project src/DisplaySwitch.App
```

[`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) covers conventions and debugging DDC/CI. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) explains how the code fits together.

## Pull requests

1. For anything larger than a small fix, open an issue first so we can agree on the approach.
2. Keep each pull request focused on one change, and give it a [Conventional Commits](https://www.conventionalcommits.org/) title (`fix: …`, `feat: …`, `docs: …`). Pull requests are squash-merged, and releases and the changelog are generated from these titles (see [docs/RELEASING.md](docs/RELEASING.md)).
3. Before pushing, run:

   ```bash
   dotnet format DisplaySwitch.slnx
   dotnet build -c Release
   dotnet test -c Release
   ```

4. Add or update tests for behaviour changes. Hardware-dependent code is tested with the simulated monitors in `tests/DisplaySwitch.TestSupport`.
5. UI changes: follow [`docs/DESIGN.md`](docs/DESIGN.md) (tokens and style classes, never inline colours), keep keyboard and screen-reader access working, and attach before/after screenshots. The UI tests write screenshots to `tests/DisplaySwitch.App.Tests/bin/<config>/net10.0/screenshots`.
6. Update the docs and `CHANGELOG.md` when behaviour changes.

### Ground rules for hardware code

- Never claim success the monitor didn't confirm.
- Never write input codes that the user hasn't configured or explicitly asked to test.
- Separate what you verified on real hardware from what you assumed, in code comments and in `docs/HARDWARE.md`.
- Don't commit anything personal from testing: logs, `settings.json`, monitor serial numbers, screenshots of your desktop.

## Platform help wanted

macOS (IOAVService) and Linux (i2c-dev) support is implemented but **not yet verified on real hardware**, and global shortcuts are Windows-only. Testing and fixes on those platforms are especially welcome.

## Licence

By contributing, you agree that your contributions are licensed under the project's [MIT License](LICENSE).

## Code of conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). Be kind and constructive.
