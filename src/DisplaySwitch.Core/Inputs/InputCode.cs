using System.Globalization;
using System.Text.Json.Serialization;

namespace DisplaySwitch.Core.Inputs;

/// <summary>
/// A value of the MCCS "Input Select" feature (VCP code 0x60) that identifies one monitor input.
/// </summary>
/// <remarks>
/// MCCS defines 0x60 as a 16-bit value, but every known monitor only uses the low byte for input
/// selection, so the domain models it as a byte. Zero is reserved by MCCS and is never a valid input.
/// </remarks>
[JsonConverter(typeof(InputCodeJsonConverter))]
public readonly record struct InputCode(byte Value)
{
    public bool IsValid => Value != 0;

    public override string ToString() => $"0x{Value:X2}";

    /// <summary>Creates a code from a raw VCP 0x60 reading, keeping only the significant low byte.</summary>
    public static InputCode FromVcpValue(ushort value) => new((byte)(value & 0xFF));

    /// <summary>Parses user input in hexadecimal (<c>0x11</c>) or decimal (<c>17</c>) notation.</summary>
    public static bool TryParse(string? text, out InputCode code)
    {
        code = default;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        var parsed = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? byte.TryParse(trimmed.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex) ? hex : (byte?)null
            : byte.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var dec) ? dec : (byte?)null;

        if (parsed is not { } value || value == 0)
        {
            return false;
        }

        code = new InputCode(value);
        return true;
    }
}
