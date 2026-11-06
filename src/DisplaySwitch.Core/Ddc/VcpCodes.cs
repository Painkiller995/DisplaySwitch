namespace DisplaySwitch.Core.Ddc;

/// <summary>VCP feature codes used by the application (VESA MCCS 2.2a).</summary>
public static class VcpCodes
{
    /// <summary>Color Temperature Increment: Kelvin per step of <see cref="ColorTemperatureRequest"/> (read-only).</summary>
    public const byte ColorTemperatureIncrement = 0x0B;

    /// <summary>Color Temperature Request: 3000 K + value × increment.</summary>
    public const byte ColorTemperatureRequest = 0x0C;

    /// <summary>Luminance (brightness), 0 to the maximum the monitor reports.</summary>
    public const byte Brightness = 0x10;

    /// <summary>Input Select: reads or changes the active video input.</summary>
    public const byte InputSelect = 0x60;
}
