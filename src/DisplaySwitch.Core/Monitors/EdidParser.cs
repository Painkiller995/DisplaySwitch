using System.Text;

namespace DisplaySwitch.Core.Monitors;

/// <summary>Identity fields from a monitor's EDID base block.</summary>
/// <param name="ManufacturerId">Three-letter PnP ID, such as "SAM".</param>
/// <param name="ProductCode">Manufacturer product code.</param>
/// <param name="ModelName">Monitor name descriptor (tag 0xFC), if present.</param>
public sealed record EdidInfo(string ManufacturerId, ushort ProductCode, string? ModelName)
{
    /// <summary>The PnP hardware ID, such as "SAM0F21", which identifies a monitor model.</summary>
    public string HardwareId => MonitorIdentity.FormatHardwareId(ManufacturerId, ProductCode);
}

/// <summary>Parses the 128-byte EDID base block (VESA E-EDID 1.4).</summary>
public static class EdidParser
{
    public const int BlockLength = 128;

    private static ReadOnlySpan<byte> Header => [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    private const int ManufacturerOffset = 8;
    private const int ProductCodeOffset = 10;
    private const int FirstDescriptorOffset = 54;
    private const int DescriptorLength = 18;
    private const int DescriptorCount = 4;
    private const byte MonitorNameTag = 0xFC;
    private const int DescriptorTextOffset = 5;
    private const int DescriptorTextLength = 13;

    public static bool TryParse(ReadOnlySpan<byte> edid, out EdidInfo info)
    {
        info = null!;
        if (edid.Length < BlockLength || !edid[..Header.Length].SequenceEqual(Header) || !HasValidChecksum(edid[..BlockLength]))
        {
            return false;
        }

        info = new EdidInfo(
            MonitorIdentity.DecodePnpId((ushort)((edid[ManufacturerOffset] << 8) | edid[ManufacturerOffset + 1])),
            (ushort)(edid[ProductCodeOffset] | (edid[ProductCodeOffset + 1] << 8)),
            ReadMonitorName(edid));
        return true;
    }

    private static bool HasValidChecksum(ReadOnlySpan<byte> block)
    {
        byte sum = 0;
        foreach (var b in block)
        {
            sum += b;
        }

        return sum == 0;
    }

    private static string? ReadMonitorName(ReadOnlySpan<byte> edid)
    {
        for (var i = 0; i < DescriptorCount; i++)
        {
            var descriptor = edid.Slice(FirstDescriptorOffset + (i * DescriptorLength), DescriptorLength);
            // Display descriptors (as opposed to timing descriptors) start with three zero bytes.
            if (descriptor[0] != 0 || descriptor[1] != 0 || descriptor[2] != 0 || descriptor[3] != MonitorNameTag)
            {
                continue;
            }

            var text = Encoding.ASCII.GetString(descriptor.Slice(DescriptorTextOffset, DescriptorTextLength));
            var newline = text.IndexOf('\n', StringComparison.Ordinal);
            var name = (newline >= 0 ? text[..newline] : text).Trim();
            return name.Length > 0 ? name : null;
        }

        return null;
    }
}
