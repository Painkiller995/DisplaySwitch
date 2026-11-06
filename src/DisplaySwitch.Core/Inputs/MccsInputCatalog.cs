namespace DisplaySwitch.Core.Inputs;

/// <summary>
/// Standard input names from the VESA MCCS 2.2a "Input Select" (VCP 0x60) table.
/// </summary>
/// <remarks>Defaults only: many monitors deviate from the table, and USB-C has no standard code.</remarks>
public static class MccsInputCatalog
{
    private static readonly Dictionary<byte, (InputKind Kind, string Name)> StandardInputs = new()
    {
        [0x01] = (InputKind.Vga, "VGA 1"),
        [0x02] = (InputKind.Vga, "VGA 2"),
        [0x03] = (InputKind.Dvi, "DVI 1"),
        [0x04] = (InputKind.Dvi, "DVI 2"),
        [0x05] = (InputKind.Analog, "Composite 1"),
        [0x06] = (InputKind.Analog, "Composite 2"),
        [0x07] = (InputKind.Analog, "S-Video 1"),
        [0x08] = (InputKind.Analog, "S-Video 2"),
        [0x09] = (InputKind.Analog, "Tuner 1"),
        [0x0A] = (InputKind.Analog, "Tuner 2"),
        [0x0B] = (InputKind.Analog, "Tuner 3"),
        [0x0C] = (InputKind.Analog, "Component 1"),
        [0x0D] = (InputKind.Analog, "Component 2"),
        [0x0E] = (InputKind.Analog, "Component 3"),
        [0x0F] = (InputKind.DisplayPort, "DisplayPort 1"),
        [0x10] = (InputKind.DisplayPort, "DisplayPort 2"),
        [0x11] = (InputKind.Hdmi, "HDMI 1"),
        [0x12] = (InputKind.Hdmi, "HDMI 2"),
    };

    /// <summary>Describes a code using the MCCS table, falling back to a neutral "Input 0xNN" name.</summary>
    public static InputSource Describe(InputCode code) =>
        StandardInputs.TryGetValue(code.Value, out var entry)
            ? new InputSource { Code = code, PortName = entry.Name, Kind = entry.Kind, Icon = DefaultIcon(entry.Kind) }
            : new InputSource { Code = code, PortName = $"Input {code}", Kind = InputKind.Other, Icon = InputIcon.Port };

    /// <summary>The icon shown before a user picks one: desktops for DisplayPort, laptops for USB-C.</summary>
    private static InputIcon DefaultIcon(InputKind kind) => kind switch
    {
        InputKind.DisplayPort => InputIcon.Desktop,
        InputKind.UsbC => InputIcon.Laptop,
        _ => InputIcon.Port,
    };
}
