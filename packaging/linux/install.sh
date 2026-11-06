#!/usr/bin/env bash
# Installs DisplaySwitch for the current user (no root needed) into ~/.local.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
data="${XDG_DATA_HOME:-$HOME/.local/share}"
target="$data/displayswitch"

rm -rf "$target"
mkdir -p "$target" "$HOME/.local/bin" "$data/applications" "$data/icons/hicolor/256x256/apps"
cp -r "$here/app/." "$target/"
ln -sf "$target/DisplaySwitch" "$HOME/.local/bin/displayswitch"
sed "s|^Exec=.*|Exec=\"$target/DisplaySwitch\"|" "$here/displayswitch.desktop" > "$data/applications/displayswitch.desktop"
cp "$here/displayswitch.png" "$data/icons/hicolor/256x256/apps/displayswitch.png"
command -v update-desktop-database > /dev/null && update-desktop-database "$data/applications" || true

echo "DisplaySwitch installed. Launch it from your applications menu or run: displayswitch"

# Monitor control needs the i2c-dev module and access to /dev/i2c-*. Both require root, so they are
# explained rather than done silently.
if ! ls /dev/i2c-* > /dev/null 2>&1; then
    echo
    echo "Monitor control needs the i2c-dev kernel module:"
    echo "  sudo modprobe i2c-dev && echo i2c-dev | sudo tee /etc/modules-load.d/i2c-dev.conf"
elif ! [ -w "$(ls /dev/i2c-* | head -n 1)" ]; then
    echo
    echo "Your user can't access /dev/i2c-* yet. Add yourself to the i2c group, then sign out and back in:"
    echo "  sudo groupadd --force --system i2c && sudo usermod -aG i2c \"$USER\""
    echo "If /dev/i2c-* is not owned by the i2c group on your distribution, see README.txt."
fi
