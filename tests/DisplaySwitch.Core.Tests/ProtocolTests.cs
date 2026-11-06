using System.Text;
using DisplaySwitch.Core.Ddc;
using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Monitors;

namespace DisplaySwitch.Core.Tests;

public class InputCodeTests
{
    [Theory]
    [InlineData("0x11", 0x11)]
    [InlineData("0X0f", 0x0F)]
    [InlineData("17", 17)]
    [InlineData(" 255 ", 255)]
    public void TryParse_accepts_hex_and_decimal(string text, byte expected)
    {
        Assert.True(InputCode.TryParse(text, out var code));
        Assert.Equal(expected, code.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("0x00")]
    [InlineData("256")]
    [InlineData("-1")]
    [InlineData("0xZZ")]
    [InlineData("HDMI")]
    public void TryParse_rejects_invalid_and_reserved_values(string? text) =>
        Assert.False(InputCode.TryParse(text, out _));

    [Fact]
    public void FromVcpValue_keeps_only_the_low_byte() =>
        Assert.Equal(new InputCode(0x11), InputCode.FromVcpValue(0x0111));

    [Fact]
    public void Catalog_describes_standard_and_unknown_codes()
    {
        Assert.Equal("HDMI 2", MccsInputCatalog.Describe(new InputCode(0x12)).PortName);
        Assert.Equal(InputKind.DisplayPort, MccsInputCatalog.Describe(new InputCode(0x0F)).Kind);
        Assert.Equal("Input 0x1B", MccsInputCatalog.Describe(new InputCode(0x1B)).PortName);
    }
}

public class CapabilitiesParserTests
{
    /// <summary>Samsung C49J89x, firmware M-J8949CGAA-1011.0.</summary>
    private const string SamsungC49J89x =
        "(prot(monitor)type(lcd)model(FALCON)cmds(01 02 03 07 0C 4E F3 E3)vcp(02 04 05 08 0B 0C 10 12 14(01 04 05 06 07 08 0A 0B) 16 18 1A 6C 6E 70 AC AE B6 C0 C6 C8 C9 CA CC(00 02 03 04 05 08 09 0A 0D) D6(01 04) DC(00 01 02 03 04) DF 60(01 03) 62 8D FF)mswhql(1)mccs_ver(2.0)asset_eep(32)mpu_ver(01))";

    [Fact]
    public void Parses_real_samsung_string()
    {
        var caps = CapabilitiesParser.Parse(SamsungC49J89x);

        Assert.Equal("FALCON", caps.Model);
        Assert.Equal("2.0", caps.MccsVersion);
        Assert.True(caps.SupportsInputSelect);
        Assert.Equal([new InputCode(0x01), new InputCode(0x03)], caps.AdvertisedInputs);
        Assert.Equal([0x01, 0x04, 0x05, 0x06, 0x07, 0x08, 0x0A, 0x0B], caps.VcpFeatures[0x14]);
        Assert.Empty(caps.VcpFeatures[0x10]);
    }

    [Fact]
    public void Parses_codes_written_without_separators()
    {
        var caps = CapabilitiesParser.Parse("(vcp(021060(0F1112)))");

        Assert.Equal([new InputCode(0x0F), new InputCode(0x11), new InputCode(0x12)], caps.AdvertisedInputs);
        Assert.True(caps.VcpFeatures.ContainsKey(0x02));
    }

    [Fact]
    public void Tolerates_missing_outer_parentheses_and_truncation()
    {
        var caps = CapabilitiesParser.Parse("prot(monitor)vcp(10 60(0F 11");

        Assert.Equal([new InputCode(0x0F), new InputCode(0x11)], caps.AdvertisedInputs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage without structure")]
    public void Returns_empty_capabilities_for_unusable_input(string? text)
    {
        var caps = CapabilitiesParser.Parse(text);

        Assert.False(caps.SupportsInputSelect);
        Assert.Empty(caps.AdvertisedInputs);
    }

    [Fact]
    public void Ignores_reserved_input_zero() =>
        Assert.Equal([new InputCode(0x11)], CapabilitiesParser.Parse("(vcp(60(00 11)))").AdvertisedInputs);
}

public class DdcProtocolTests
{
    [Fact]
    public void Get_vcp_request_matches_ddc_ci_specification() =>
        // Reference frame from the DDC/CI 1.1 specification: Get VCP Feature for brightness (0x10).
        Assert.Equal(new byte[] { 0x51, 0x82, 0x01, 0x10, 0xAC }, DdcProtocol.BuildGetVcpRequest(0x10));

    [Fact]
    public void Set_vcp_request_encodes_value_big_endian_with_checksum()
    {
        var frame = DdcProtocol.BuildSetVcpRequest(0x60, 0x0111);

        Assert.Equal(new byte[] { 0x51, 0x84, 0x03, 0x60, 0x01, 0x11 }, frame[..^1]);
        Assert.Equal(Xor(0x6E, frame.AsSpan(..^1)), frame[^1]);
    }

    [Fact]
    public void Parses_valid_get_vcp_reply()
    {
        var reply = Reply(0x02, 0x00, 0x60, 0x00, 0x00, 0x12, 0x00, 0x0F);

        var reading = DdcProtocol.ParseGetVcpReply(reply, 0x60);

        Assert.Equal(0x0F, reading.Current);
        Assert.Equal(0x12, reading.Maximum);
    }

    [Fact]
    public void Rejects_reply_with_bad_checksum()
    {
        var reply = Reply(0x02, 0x00, 0x60, 0x00, 0x00, 0x12, 0x00, 0x0F);
        reply[^1] ^= 0xFF;

        Assert.Equal(DdcError.InvalidReply, Assert.Throws<DdcException>(() => DdcProtocol.ParseGetVcpReply(reply, 0x60)).Error);
    }

    [Fact]
    public void Reports_unsupported_feature()
    {
        var reply = Reply(0x02, 0x01, 0x60, 0x00, 0x00, 0x00, 0x00, 0x00);

        Assert.Equal(DdcError.Unsupported, Assert.Throws<DdcException>(() => DdcProtocol.ParseGetVcpReply(reply, 0x60)).Error);
    }

    [Fact]
    public void Treats_null_message_as_no_response()
    {
        byte[] nullMessage = [0x6E, 0x80, Xor(0x50, [0x6E, 0x80])];

        Assert.Equal(DdcError.NoResponse, Assert.Throws<DdcException>(() => DdcProtocol.ParseGetVcpReply(nullMessage, 0x60)).Error);
    }

    [Fact]
    public void Rejects_reply_for_another_feature()
    {
        var reply = Reply(0x02, 0x00, 0x10, 0x00, 0x00, 0x64, 0x00, 0x32);

        Assert.Throws<DdcException>(() => DdcProtocol.ParseGetVcpReply(reply, 0x60));
    }

    [Fact]
    public void Parses_capabilities_fragment_and_end_marker()
    {
        var fragment = DdcProtocol.ParseCapabilitiesReply(Reply(0xE3, 0x00, 0x20, (byte)'(', (byte)'v'), 0x20);
        var end = DdcProtocol.ParseCapabilitiesReply(Reply(0xE3, 0x00, 0x22), 0x22);

        Assert.Equal("(v"u8.ToArray(), fragment.ToArray());
        Assert.True(end.IsEmpty);
    }

    private static byte[] Reply(params byte[] payload)
    {
        byte[] frame = [0x6E, (byte)(0x80 | payload.Length), .. payload];
        return [.. frame, Xor(0x50, frame)];
    }

    private static byte Xor(byte seed, ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            seed ^= b;
        }

        return seed;
    }
}

public class EdidParserTests
{
    [Fact]
    public void Parses_manufacturer_product_and_name()
    {
        Assert.True(EdidParser.TryParse(BuildEdid("SAM", 0x0F21, "C49J89x"), out var info));

        Assert.Equal("SAM", info.ManufacturerId);
        Assert.Equal(0x0F21, info.ProductCode);
        Assert.Equal("C49J89x", info.ModelName);
        Assert.Equal("SAM0F21", info.HardwareId);
    }

    [Fact]
    public void Rejects_corrupt_checksum()
    {
        var edid = BuildEdid("DEL", 0x41A8, "DELL U2720Q");
        edid[20] ^= 0x01;

        Assert.False(EdidParser.TryParse(edid, out _));
    }

    [Fact]
    public void Rejects_short_or_headerless_data()
    {
        Assert.False(EdidParser.TryParse(new byte[64], out _));
        Assert.False(EdidParser.TryParse(new byte[128], out _));
    }

    private static byte[] BuildEdid(string pnp, ushort product, string name)
    {
        var edid = new byte[128];
        byte[] header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
        header.CopyTo(edid, 0);
        var packed = ((pnp[0] - '@') << 10) | ((pnp[1] - '@') << 5) | (pnp[2] - '@');
        edid[8] = (byte)(packed >> 8);
        edid[9] = (byte)packed;
        edid[10] = (byte)product;
        edid[11] = (byte)(product >> 8);
        edid[54 + 3] = 0xFC; // First descriptor holds the monitor name.
        Encoding.ASCII.GetBytes((name + "\n").PadRight(13)).CopyTo(edid, 54 + 5);
        byte sum = 0;
        for (var i = 0; i < 127; i++)
        {
            sum += edid[i];
        }

        edid[127] = (byte)(256 - sum);
        return edid;
    }
}

public class MonitorIdentityTests
{
    [Fact]
    public void Decodes_pnp_id() => Assert.Equal("SAM", MonitorIdentity.DecodePnpId(0x4C2D));

    [Fact]
    public void Assigns_distinct_keys_to_identical_monitors() =>
        Assert.Equal(["DEL41A8", "SAM0F21", "DEL41A8#2", "UNKNOWN"], MonitorIdentity.AssignKeys(["DEL41A8", "SAM0F21", "DEL41A8", ""]));
}
