namespace DisplaySwitch.Core.Ddc;

/// <summary>
/// Encodes and decodes raw DDC/CI messages, as defined by VESA DDC/CI 1.1.
/// </summary>
/// <remarks>
/// Windows hides this layer behind dxva2.dll. Linux (i2c-dev) and macOS (IOAVService) only expose
/// raw I²C, so the platform adapters there use this codec.
/// <para>
/// A host-to-display request is written to I²C address 0x37 as:
/// <c>[0x51 source] [0x80 | length] [payload…] [checksum]</c>, where the checksum is the XOR of the
/// destination address byte 0x6E and every preceding byte.
/// A display reply reads back as <c>[0x6E] [0x80 | length] [payload…] [checksum]</c>, checksummed
/// with the virtual host address 0x50 as the seed.
/// </para>
/// </remarks>
public static class DdcProtocol
{
    /// <summary>7-bit I²C address of the DDC/CI interface.</summary>
    public const byte DdcCiAddress = 0x37;

    /// <summary>7-bit I²C address of the EDID EEPROM.</summary>
    public const byte EdidAddress = 0x50;

    /// <summary>Source address byte that starts every host request ("sub-address" in IOAVService terms).</summary>
    public const byte HostSourceAddress = 0x51;

    /// <summary>Recommended wait between a request and reading its reply (DDC/CI 1.1 §4.3).</summary>
    public static readonly TimeSpan ReplyDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>Read buffer large enough for any reply this codec understands (largest is a 32-byte capabilities fragment).</summary>
    public const int MaxReplyLength = 40;

    /// <summary>Length of a Get VCP Feature reply frame.</summary>
    public const int GetVcpReplyLength = 11;

    private const byte DisplayDestinationAddress = 0x6E;
    private const byte ReplyChecksumSeed = 0x50;
    private const byte LengthFlag = 0x80;
    private const byte GetVcpRequestOpcode = 0x01;
    private const byte GetVcpReplyOpcode = 0x02;
    private const byte SetVcpOpcode = 0x03;
    private const byte CapabilitiesRequestOpcode = 0xF3;
    private const byte CapabilitiesReplyOpcode = 0xE3;
    private const byte VcpResultNoError = 0x00;

    public static byte[] BuildGetVcpRequest(byte vcpCode) => Frame([GetVcpRequestOpcode, vcpCode]);

    public static byte[] BuildSetVcpRequest(byte vcpCode, ushort value) =>
        Frame([SetVcpOpcode, vcpCode, (byte)(value >> 8), (byte)value]);

    public static byte[] BuildCapabilitiesRequest(ushort offset) =>
        Frame([CapabilitiesRequestOpcode, (byte)(offset >> 8), (byte)offset]);

    /// <summary>Decodes a Get VCP Feature reply.</summary>
    /// <exception cref="DdcException">The reply is malformed, corrupt, or reports an error.</exception>
    public static VcpReading ParseGetVcpReply(ReadOnlySpan<byte> reply, byte expectedVcpCode)
    {
        var payload = ValidateReply(reply);
        if (payload.Length != 8 || payload[0] != GetVcpReplyOpcode)
        {
            throw new DdcException(DdcError.InvalidReply, "The monitor sent an unexpected reply to a Get VCP request.");
        }

        if (payload[1] != VcpResultNoError)
        {
            throw new DdcException(DdcError.Unsupported, $"The monitor does not support VCP feature 0x{expectedVcpCode:X2}.");
        }

        if (payload[2] != expectedVcpCode)
        {
            throw new DdcException(DdcError.InvalidReply, $"The monitor replied for VCP 0x{payload[2]:X2} instead of 0x{expectedVcpCode:X2}.");
        }

        return new VcpReading(
            Current: (ushort)((payload[6] << 8) | payload[7]),
            Maximum: (ushort)((payload[4] << 8) | payload[5]));
    }

    /// <summary>Decodes one capabilities fragment. An empty result marks the end of the string.</summary>
    /// <exception cref="DdcException">The reply is malformed or for a different offset.</exception>
    public static ReadOnlySpan<byte> ParseCapabilitiesReply(ReadOnlySpan<byte> reply, ushort expectedOffset)
    {
        var payload = ValidateReply(reply);
        if (payload.Length < 3 || payload[0] != CapabilitiesReplyOpcode)
        {
            throw new DdcException(DdcError.InvalidReply, "The monitor sent an unexpected reply to a capabilities request.");
        }

        var offset = (ushort)((payload[1] << 8) | payload[2]);
        if (offset != expectedOffset)
        {
            throw new DdcException(DdcError.InvalidReply, $"The monitor returned capabilities offset {offset} instead of {expectedOffset}.");
        }

        return payload[3..];
    }

    private static byte[] Frame(ReadOnlySpan<byte> payload)
    {
        var frame = new byte[payload.Length + 3];
        frame[0] = HostSourceAddress;
        frame[1] = (byte)(LengthFlag | payload.Length);
        payload.CopyTo(frame.AsSpan(2));
        frame[^1] = Checksum(DisplayDestinationAddress, frame.AsSpan(0, frame.Length - 1));
        return frame;
    }

    private static ReadOnlySpan<byte> ValidateReply(ReadOnlySpan<byte> reply)
    {
        if (reply.Length < 3 || reply[0] != DisplayDestinationAddress || (reply[1] & LengthFlag) == 0)
        {
            throw new DdcException(DdcError.InvalidReply, "The monitor reply has an invalid header.");
        }

        var length = reply[1] & ~LengthFlag;
        if (length == 0)
        {
            // A "null message" means the display is busy or has nothing to report yet.
            throw new DdcException(DdcError.NoResponse, "The monitor is busy and did not reply.");
        }

        if (reply.Length < length + 3)
        {
            throw new DdcException(DdcError.InvalidReply, "The monitor reply is truncated.");
        }

        var frame = reply[..(length + 2)];
        if (Checksum(ReplyChecksumSeed, frame) != reply[length + 2])
        {
            throw new DdcException(DdcError.InvalidReply, "The monitor reply failed its checksum.");
        }

        return frame[2..];
    }

    private static byte Checksum(byte seed, ReadOnlySpan<byte> bytes)
    {
        var checksum = seed;
        foreach (var b in bytes)
        {
            checksum ^= b;
        }

        return checksum;
    }
}
