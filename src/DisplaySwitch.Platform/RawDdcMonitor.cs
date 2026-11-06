using System.Text;
using DisplaySwitch.Core.Ddc;
using DisplaySwitch.Core.Monitors;

namespace DisplaySwitch.Platform;

/// <summary>
/// Implements DDC/CI on top of a raw I²C transport. Linux and macOS expose only raw I²C, so both
/// use this class with their own transport.
/// </summary>
internal sealed class RawDdcMonitor(MonitorIdentity identity, IRawDdcTransport? transport) : IDdcMonitor
{
    /// <summary>Capabilities strings are a few hundred bytes; this bounds a monitor that never ends the string.</summary>
    private const int MaxCapabilitiesLength = 4096;

    public MonitorIdentity Identity { get; } = identity;

    public bool SupportsDdc => transport is not null && Identity.Connection != ConnectionKind.Internal;

    public string ReadCapabilities()
    {
        var link = RequireTransport();
        var text = new StringBuilder();
        var reply = new byte[DdcProtocol.MaxReplyLength];
        while (text.Length < MaxCapabilitiesLength)
        {
            var offset = (ushort)text.Length;
            link.Transfer(DdcProtocol.BuildCapabilitiesRequest(offset), reply);
            var fragment = DdcProtocol.ParseCapabilitiesReply(reply, offset);
            if (fragment.IsEmpty)
            {
                break;
            }

            text.Append(Encoding.ASCII.GetString(fragment));
        }

        return text.ToString().TrimEnd('\0');
    }

    public VcpReading ReadVcp(byte vcpCode)
    {
        var reply = new byte[DdcProtocol.GetVcpReplyLength];
        RequireTransport().Transfer(DdcProtocol.BuildGetVcpRequest(vcpCode), reply);
        return DdcProtocol.ParseGetVcpReply(reply, vcpCode);
    }

    public void WriteVcp(byte vcpCode, ushort value) =>
        RequireTransport().Transfer(DdcProtocol.BuildSetVcpRequest(vcpCode, value), []);

    public void Dispose() => transport?.Dispose();

    private IRawDdcTransport RequireTransport() =>
        transport ?? throw new DdcException(DdcError.AccessDenied, "This monitor's DDC/CI bus is not accessible.");
}

/// <summary>Sends a framed DDC/CI request (starting with the 0x51 source byte) and reads the reply.</summary>
internal interface IRawDdcTransport : IDisposable
{
    void Transfer(ReadOnlySpan<byte> request, Span<byte> reply);
}
