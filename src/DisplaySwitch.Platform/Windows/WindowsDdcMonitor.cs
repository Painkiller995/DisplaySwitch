using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using DisplaySwitch.Core.Ddc;
using DisplaySwitch.Core.Monitors;
using DisplaySwitch.Platform.Windows.Native;

namespace DisplaySwitch.Platform.Windows;

/// <summary>DDC/CI access to one physical monitor through dxva2.dll.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsDdcMonitor(SafePhysicalMonitorHandle handle, MonitorIdentity identity) : IDdcMonitor
{
    public MonitorIdentity Identity { get; } = identity;

    public bool SupportsDdc => Identity.Connection is not (ConnectionKind.Internal or ConnectionKind.Wireless);

    public string ReadCapabilities()
    {
        if (!Dxva2.GetCapabilitiesStringLength(handle, out var length))
        {
            throw LastError("read the capabilities length");
        }

        var buffer = new byte[length];
        if (!Dxva2.CapabilitiesRequestAndCapabilitiesReply(handle, buffer, length))
        {
            throw LastError("read capabilities");
        }

        return Encoding.ASCII.GetString(buffer).TrimEnd('\0');
    }

    public VcpReading ReadVcp(byte vcpCode)
    {
        if (!Dxva2.GetVCPFeatureAndVCPFeatureReply(handle, vcpCode, out _, out var current, out var maximum))
        {
            throw LastError($"read VCP 0x{vcpCode:X2}");
        }

        return new VcpReading((ushort)current, (ushort)maximum);
    }

    public void WriteVcp(byte vcpCode, ushort value)
    {
        if (!Dxva2.SetVCPFeature(handle, vcpCode, value))
        {
            throw LastError($"write VCP 0x{vcpCode:X2}");
        }
    }

    public void Dispose() => handle.Dispose();

    private static DdcException LastError(string operation)
    {
        var code = unchecked((uint)Marshal.GetLastPInvokeError());
        return new DdcException(Classify(code), $"Could not {operation} (error 0x{code:X8}).");
    }

    /// <summary>Maps the ERROR_GRAPHICS_* codes from winerror.h (0xC0262580–0xC026258D) to <see cref="DdcError"/>.</summary>
    private static DdcError Classify(uint code) => code switch
    {
        0xC0262580 or 0xC0262581 => DdcError.Unsupported,   // I2C_NOT_SUPPORTED, I2C_DEVICE_DOES_NOT_EXIST
        0xC0262582 or 0xC0262583 => DdcError.NoResponse,    // I2C_ERROR_TRANSMITTING_DATA, I2C_ERROR_RECEIVING_DATA
        0xC0262584 => DdcError.Unsupported,                  // DDCCI_VCP_NOT_SUPPORTED
        >= 0xC0262585 and <= 0xC026258B => DdcError.InvalidReply, // invalid data/message/checksum/capabilities
        0xC026258C or 0xC026258D or 6 => DdcError.Disconnected,   // INVALID_PHYSICAL_MONITOR_HANDLE, MONITOR_NO_LONGER_EXISTS, ERROR_INVALID_HANDLE
        _ => DdcError.NoResponse,
    };
}
