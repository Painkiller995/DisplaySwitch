using DisplaySwitch.Core.Inputs;

namespace DisplaySwitch.Core.Profiles;

/// <summary>Input definitions for monitors whose capabilities string is wrong. Evidence: docs/HARDWARE.md.</summary>
public static class MonitorProfiles
{
    private static readonly Dictionary<string, MonitorProfile> KnownProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        // Samsung C49J89x (firmware M-J8949CGAA-1011.0) advertises 60(01 03): 0x03 is DisplayPort;
        // 0x01, the only other value, is assumed to be HDMI.
        ["SAM0F21"] = new MonitorProfile(
            "SAM0F21",
            "Samsung C49J89x",
            [
                new InputSource { Code = new InputCode(0x03), PortName = "DisplayPort", Kind = InputKind.DisplayPort, Icon = InputIcon.Desktop },
                new InputSource { Code = new InputCode(0x01), PortName = "HDMI", Kind = InputKind.Hdmi, Icon = InputIcon.Port },
            ]),
    };

    public static MonitorProfile? Find(string hardwareId) =>
        KnownProfiles.GetValueOrDefault(hardwareId);
}
