#!/usr/bin/env bash
# Builds a Linux release tarball: a self-contained app plus a per-user install script.
# Usage: packaging/linux/build.sh [linux-x64|linux-arm64]
set -euo pipefail

rid="${1:-linux-x64}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
project="$root/src/DisplaySwitch.App/DisplaySwitch.App.csproj"
version="$(dotnet msbuild "$project" -getProperty:Version | tr -d '[:space:]')"
name="DisplaySwitch-$version-$rid"
stage="$root/artifacts/linux/$name"
out="$root/artifacts/installer"

rm -rf "$stage"
mkdir -p "$stage" "$out"
dotnet publish "$project" -c Release -r "$rid" -o "$stage/app"

cp "$root/packaging/linux/displayswitch.desktop" "$stage/"
cp "$root/packaging/icons/displayswitch-256.png" "$stage/displayswitch.png"
cp "$root/packaging/linux/install.sh" "$root/packaging/linux/uninstall.sh" "$stage/"
cp "$root/packaging/linux/README.txt" "$stage/"
chmod +x "$stage/install.sh" "$stage/uninstall.sh" "$stage/app/DisplaySwitch"

tar -C "$root/artifacts/linux" -czf "$out/$name.tar.gz" "$name"
(cd "$out" && sha256sum "$name.tar.gz" > "$name.tar.gz.sha256")
echo "Built $out/$name.tar.gz"
