#!/usr/bin/env bash
# Builds DisplaySwitch.app and a .dmg for macOS. Run on macOS (needs codesign and hdiutil).
# Usage: packaging/macos/build.sh [osx-arm64|osx-x64]
#
# Signing: the bundle is ad-hoc signed, which Apple silicon requires to run at all. Distribution
# without a Gatekeeper warning additionally needs a Developer ID certificate and notarization; set
# MACOS_SIGN_IDENTITY to sign with one. Notarization is not automated here.
set -euo pipefail

rid="${1:-osx-arm64}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
project="$root/src/DisplaySwitch.App/DisplaySwitch.App.csproj"
version="$(dotnet msbuild "$project" -getProperty:Version | tr -d '[:space:]')"
stage="$root/artifacts/macos/$rid"
bundle="$stage/DisplaySwitch.app"
out="$root/artifacts/installer"
dmg="$out/DisplaySwitch-$version-$rid.dmg"

rm -rf "$stage"
mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources" "$out"
dotnet publish "$project" -c Release -r "$rid" -o "$bundle/Contents/MacOS"

sed "s/__VERSION__/$version/g" "$root/packaging/macos/Info.plist" > "$bundle/Contents/Info.plist"
cp "$root/packaging/icons/displayswitch.icns" "$bundle/Contents/Resources/"

identity="${MACOS_SIGN_IDENTITY:--}"
codesign --force --deep --options runtime --sign "$identity" "$bundle"
if [ "$identity" = "-" ]; then
    echo "NOTE: ad-hoc signed only. Users must right-click > Open on first launch (not notarized)."
fi

rm -f "$dmg"
hdiutil create -volname "DisplaySwitch" -srcfolder "$bundle" -ov -format UDZO "$dmg"
(cd "$out" && shasum -a 256 "$(basename "$dmg")" > "$(basename "$dmg").sha256")
echo "Built $dmg"
