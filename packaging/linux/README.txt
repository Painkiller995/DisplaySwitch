DisplaySwitch for Linux
=======================

Install for your user:     ./install.sh
Remove:                    ./uninstall.sh
Run without installing:    ./app/DisplaySwitch

Monitor control
---------------
DisplaySwitch talks to monitors over DDC/CI through the kernel's i2c-dev interface (the same
mechanism ddcutil uses). It needs:

1. The i2c-dev module:
       sudo modprobe i2c-dev
       echo i2c-dev | sudo tee /etc/modules-load.d/i2c-dev.conf     # load at boot

2. Permission to use /dev/i2c-*. Most distributions grant it to the "i2c" group:
       sudo groupadd --force --system i2c
       sudo usermod -aG i2c "$USER"
   then sign out and back in. If your distribution does not assign /dev/i2c-* to that group,
   add a udev rule:
       echo 'KERNEL=="i2c-[0-9]*", GROUP="i2c", MODE="0660"' | sudo tee /etc/udev/rules.d/45-displayswitch-i2c.rules
       sudo udevadm control --reload && sudo udevadm trigger

3. DDC/CI turned on in the monitor's on-screen menu.

Known limitations
-----------------
- Global keyboard shortcuts are not available on Linux yet (Wayland does not allow applications
  to grab keys globally). Use the tray icon's menu to switch quickly.
- The proprietary NVIDIA driver does not expose monitors through /sys/class/drm, so monitors on
  it are not detected.
