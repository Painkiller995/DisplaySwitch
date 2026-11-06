#!/usr/bin/env bash
# Removes a per-user DisplaySwitch installation. Settings in ~/.config/displayswitch are kept.
set -euo pipefail

data="${XDG_DATA_HOME:-$HOME/.local/share}"
config="${XDG_CONFIG_HOME:-$HOME/.config}"

pkill -x DisplaySwitch 2> /dev/null || true
rm -rf "$data/displayswitch"
rm -f "$HOME/.local/bin/displayswitch" \
      "$data/applications/displayswitch.desktop" \
      "$data/icons/hicolor/256x256/apps/displayswitch.png" \
      "$config/autostart/displayswitch.desktop"
echo "DisplaySwitch removed. Your settings remain in $config/displayswitch (delete it to remove them too)."
