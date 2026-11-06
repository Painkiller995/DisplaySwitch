using System.Globalization;

namespace DisplaySwitch.Core.Monitors;

/// <summary>How the computer is physically connected to a monitor.</summary>
public enum ConnectionKind
{
    Unknown,
    DisplayPort,
    Hdmi,
    Dvi,
    Vga,
    Internal,
    Wireless,
}

/// <summary>Describes a physical monitor in user-facing and stable terms.</summary>
/// <param name="Key">
/// Stable identifier used to remember preferences across sessions: the hardware ID, with a suffix
/// when several identical monitors are attached (for example "SAM0F21" or "DEL41A8#2").
/// </param>
/// <param name="HardwareId">The PnP manufacturer and product code, or empty when unknown.</param>
/// <param name="Name">The monitor's own model name, such as "C49J89x".</param>
/// <param name="Connection">The link between this computer and the monitor.</param>
/// <param name="Width">Current desktop width in pixels, or 0 when unknown.</param>
/// <param name="Height">Current desktop height in pixels, or 0 when unknown.</param>
public sealed record MonitorIdentity(
    string Key,
    string HardwareId,
    string Name,
    ConnectionKind Connection,
    int Width,
    int Height)
{
    public bool HasResolution => Width > 0 && Height > 0;

    public static string FormatHardwareId(string manufacturerId, ushort productCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{manufacturerId}{productCode:X4}");

    /// <summary>
    /// Decodes the EDID manufacturer field: three 5-bit letters packed big-endian, where 1 = 'A'.
    /// </summary>
    public static string DecodePnpId(ushort packedBigEndian) => string.Create(3, packedBigEndian, static (chars, packed) =>
    {
        chars[0] = (char)('@' + ((packed >> 10) & 0x1F));
        chars[1] = (char)('@' + ((packed >> 5) & 0x1F));
        chars[2] = (char)('@' + (packed & 0x1F));
    });

    /// <summary>
    /// Builds stable keys for a set of hardware IDs, numbering duplicates by enumeration order so two
    /// identical monitors keep separate preferences.
    /// </summary>
    public static IReadOnlyList<string> AssignKeys(IReadOnlyList<string> hardwareIds)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var keys = new string[hardwareIds.Count];
        for (var i = 0; i < hardwareIds.Count; i++)
        {
            var baseKey = string.IsNullOrEmpty(hardwareIds[i]) ? "UNKNOWN" : hardwareIds[i];
            var occurrence = seen[baseKey] = seen.GetValueOrDefault(baseKey) + 1;
            keys[i] = occurrence == 1 ? baseKey : string.Create(CultureInfo.InvariantCulture, $"{baseKey}#{occurrence}");
        }

        return keys;
    }
}
