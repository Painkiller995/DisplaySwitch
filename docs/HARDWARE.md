# Hardware compatibility

DisplaySwitch only claims what has been observed. Each entry separates **verified on real hardware** from **assumed**.

## Samsung C49J89x (LC49J890DKRXEN): `SAM0F21`

Test setup (November 2026): firmware M-J8949CGAA-1011.0; Windows 11 (build 26300); PC connected over **DisplayPort**; single-picture mode (PIP/PBP off); a second device on HDMI.

| Observation | Status |
|---|---|
| Model name `C49J89x`, EDID ID `SAM0F21`, DisplayPort connection, 3840 × 1080 | ✅ verified |
| Capabilities string reads successfully after retries. It advertises `model(FALCON)`, MCCS 2.0 and `60(01 03)` (a vendor-specific two-value mapping) | ✅ verified |
| Firmware advertises VCP `0x60(01 03)` and reads back **`0x03` while on DisplayPort** (standard MCCS would be `0x0F`) | ✅ verified |
| DDC/CI is unreliable: about one request in four fails (`0xC0262582`, `0xC0262583`, `0xC0262589`, or a reading of `0`) in bursts of about a second | ✅ verified (handled by retries with exponential backoff) |
| One VCP read takes about 60 ms | ✅ verified |
| Writing `0x60` over the DisplayPort link is **accepted but ignored**. Codes tried: `0x11`, `0x12`, `0x05`, `0x06`, `0x1B`, `0x19`, `0x01`, `0x02`, `0x04`. The monitor stayed on DisplayPort every time, confirmed visually by the user and by read-back | ✅ verified |
| The only alternate source value advertised by firmware is `0x01` | ⚠️ likely the HDMI port; needs a real-hardware confirmation |
| USB-C input code | ❓ unknown (no standard code exists) |
| Brightness (`0x10`, 0–100): read and write | ✅ verified (write applied and read back) |
| Color temperature request (`0x0C`, 0–170): read | ✅ verified |
| Color temperature request: write | ❌ accepted but ignored in the tested picture mode; the app reports this |
| Color temperature step (`0x0B`), color preset (`0x14`), contrast (`0x12`) | ❌ fail to read; color temperature is therefore shown as warm/neutral/cool rather than Kelvin |
| Input switching works when commands come from a computer on the **HDMI** input | ❓ unverified. Owners of other Samsung models report that Samsung only honours input changes over HDMI; this matches the observation above |

**What the app does for this monitor:** the built-in profile sends the two values declared by this firmware: `0x03` for DisplayPort and `0x01` for HDMI. It verifies the monitor's reply and reports a failed switch honestly.

**To verify more codes** (for example from an HDMI-connected computer): use **Add input › Test this code**. The monitor switches for 6 seconds and is then sent back automatically. Please record results here.

## General behaviour

- **Monitors that ignore DDC/CI** are listed as *not responding*, with instructions to turn DDC/CI on in the on-screen menu.
- **Built-in laptop panels** never support DDC/CI and are labelled as such.
- **Docks, KVMs, adapters and some USB-C hubs** often do not pass DDC/CI through.
- **After switching away**, many monitors stop answering the computer they left. DisplaySwitch reports this as *"Switch command sent"* rather than claiming success.
- **No signal on the target input:** some monitors return to the previous input automatically ("auto source"). DisplaySwitch reports that the monitor stayed on its previous input.

## Common MCCS input codes

| Code | Input | Code | Input |
|---|---|---|---|
| `0x01` | VGA 1 | `0x0F` (15) | DisplayPort 1 |
| `0x03` | DVI 1 | `0x10` (16) | DisplayPort 2 |
| `0x04` | DVI 2 | `0x11` (17) | HDMI 1 |
| | | `0x12` (18) | HDMI 2 |

Manufacturers frequently deviate from this table, and USB-C has no standard code. Treat it as a starting point only.

## Adding a profile

A profile belongs in `src/DisplaySwitch.Core/Profiles/MonitorProfiles.cs` only when:

1. the monitor's capabilities string is missing or wrong, and
2. the codes in the profile were verified on that model, with the evidence recorded in this file.
