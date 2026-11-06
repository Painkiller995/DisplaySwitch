# Releasing

Releases are automatic. Commit messages determine the next version, and merging the release PR publishes installers for every platform.

## How it works

1. Pull requests are squash-merged, and their titles follow [Conventional Commits](https://www.conventionalcommits.org/). The `PR title` check enforces this.
2. On every push to `main`, [Release Please](https://github.com/googleapis/release-please) opens or updates a **release PR**. It bumps `version.txt` and adds the new entries to `CHANGELOG.md`.
3. **Merging the release PR** tags `vX.Y.Z` and creates the GitHub Release. The same workflow then runs every test on Windows, Linux and macOS, builds all packages, and attaches them to the release.

| Title prefix | Example | Next version |
|---|---|---|
| `fix:` | `fix: retry capabilities read after timeout` | patch, 1.0.0 → 1.0.1 |
| `feat:` | `feat: add USB-C profile for Dell U2723QE` | minor, 1.0.0 → 1.1.0 |
| `feat!:` or a `BREAKING CHANGE:` footer | `feat!: new settings format` | major, 1.0.0 → 2.0.0 |
| `docs:`, `test:`, `ci:`, `build:`, `refactor:`, `chore:` | `docs: update hardware table` | no release |

`version.txt` is the single source of the version; `Directory.Build.props` reads it.

## Published assets

| Platform | Files |
|---|---|
| Windows x64 | `DisplaySwitch-<v>-win-x64-setup.exe`, `DisplaySwitch-<v>-win-x64-portable.zip`, `SHA256SUMS.txt` |
| macOS | `DisplaySwitch-<v>-osx-arm64.dmg`, `DisplaySwitch-<v>-osx-x64.dmg` (+ `.sha256`) |
| Linux | `DisplaySwitch-<v>-linux-x64.tar.gz`, `DisplaySwitch-<v>-linux-arm64.tar.gz` (+ `.sha256`) |

## One-time repository setup

In the GitHub repository settings:

1. **Actions › General › Workflow permissions:** select *Read and write permissions* and tick *Allow GitHub Actions to create and approve pull requests*. Release Please needs both.
2. **General › Pull Requests:** allow *squash merging* only, with the default commit message set to *Pull request title*.
3. Optional, **Secrets and variables › Actions**, for a signed Windows build:
   - `WINDOWS_SIGNING_CERT_BASE64`: the `.pfx` file, base64-encoded (`[Convert]::ToBase64String([IO.File]::ReadAllBytes('cert.pfx'))`)
   - `WINDOWS_SIGNING_CERT_PASSWORD`

   Without them the builds are unsigned, and the build log says so.

## First release (1.0.0)

1.0.0 predates the release PR, so publish it by pushing its tag:

```bash
git tag v1.0.0
git push origin v1.0.0
```

The workflow tests, packages and publishes it, with notes taken from the 1.0.0 section of `CHANGELOG.md`. Release Please handles everything after that.

## Manual and emergency releases

Pushing any `v*` tag publishes that version, provided the tag matches `version.txt` (the workflow refuses a mismatch). Tags with a suffix, such as `v1.2.0-beta.1`, are published as pre-releases.

To test packaging without releasing, run the **Package** workflow from the Actions tab.

## Local builds

| Platform | Command | Runs on |
|---|---|---|
| Windows | `./packaging/windows/build.ps1` | Windows with Inno Setup 6 |
| Linux | `bash packaging/linux/build.sh linux-x64` | Linux or macOS |
| macOS | `bash packaging/macos/build.sh osx-arm64` | macOS |

Outputs go to `artifacts/installer/`.

## Signing

Builds are unsigned unless configured; never describe an unsigned build as signed.

- **Windows:** set the two secrets above (in CI), or `SIGN_CERT_PATH` and `SIGN_CERT_PASSWORD` locally. Unsigned builds trigger a SmartScreen warning.
- **macOS:** bundles are ad-hoc signed. Distribution without a Gatekeeper warning requires a Developer ID certificate (`MACOS_SIGN_IDENTITY`) and notarization, which is not automated.
- **Linux:** tarballs ship with SHA-256 checksums.

## Pre-release validation (Windows)

On a clean machine: install with defaults, upgrade while the app is running, and uninstall while it is running. Then confirm that the files, shortcut and startup entry are removed and that the settings in `%APPDATA%\DisplaySwitch` remain. Last performed for 1.0.0 (see `docs/TESTING.md`).

The installer's `AppId` must never change; Windows uses it to recognise upgrades.
