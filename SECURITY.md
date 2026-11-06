# Security policy

## Supported versions

Security fixes are made for the latest released version.

## Reporting a vulnerability

**Please don't report security problems in public issues.** Use GitHub's private reporting instead: go to the repository's **Security** tab and choose **Report a vulnerability**. You'll get a response within a few days and will be kept informed until a fix is released. You'll be credited unless you prefer otherwise.

## What's in scope

DisplaySwitch runs as the signed-in user, needs no administrator rights, sends no data over the network, and stores settings and logs only in the user's profile. Relevant reports include, for example:

- a way for another user, process or file to make DisplaySwitch execute code or write arbitrary DDC/CI values;
- unsafe handling of the settings file (it is treated as untrusted input);
- the installer or uninstaller modifying anything outside the app's own folders and its startup entry;
- the single-instance pipe accepting requests from other users.

## Design notes for reviewers

- The app only writes input codes that the user has configured or explicitly chose to test (`MonitorController` rejects anything else).
- The settings file is sanitised on load; invalid entries are dropped.
- The named pipe is created with `PipeOptions.CurrentUserOnly`, and its name is a hash, so the user name isn't exposed.
- Release builds are unsigned unless a signing certificate is configured (see `docs/RELEASING.md`). Verify downloads against the published SHA-256 checksums.
