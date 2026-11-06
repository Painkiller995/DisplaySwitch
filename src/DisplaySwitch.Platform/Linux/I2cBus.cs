using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DisplaySwitch.Core.Ddc;

namespace DisplaySwitch.Platform.Linux;

/// <summary>An open <c>/dev/i2c-N</c> adapter that talks to a monitor's EDID and DDC/CI addresses.</summary>
[SupportedOSPlatform("linux")]
internal sealed class I2cBus : IRawDdcTransport
{
    private readonly SafeFileDescriptor _descriptor;

    private I2cBus(SafeFileDescriptor descriptor, string path)
    {
        _descriptor = descriptor;
        Path = path;
    }

    public string Path { get; }

    /// <summary>Opens an adapter, or returns the errno explaining why it could not be opened.</summary>
    public static I2cBus? TryOpen(string path, out int errno)
    {
        var descriptor = LibC.Open(path, LibC.ReadWrite);
        errno = descriptor < 0 ? Marshal.GetLastPInvokeError() : 0;
        return descriptor < 0 ? null : new I2cBus(new SafeFileDescriptor(descriptor), path);
    }

    /// <summary>Sends a DDC/CI request and, when <paramref name="reply"/> is non-empty, reads the answer.</summary>
    public void Transfer(ReadOnlySpan<byte> request, Span<byte> reply)
    {
        Select(DdcProtocol.DdcCiAddress);
        Write(request);
        if (reply.IsEmpty)
        {
            return;
        }

        Thread.Sleep(DdcProtocol.ReplyDelay);
        Read(reply);
    }

    public void Dispose() => _descriptor.Dispose();

    private void Select(byte address)
    {
        if (LibC.IoControl(_descriptor, LibC.I2cSlave, address) < 0)
        {
            throw Failure($"select I²C address 0x{address:X2}");
        }
    }

    private unsafe void Write(ReadOnlySpan<byte> data)
    {
        fixed (byte* pointer = data)
        {
            if (LibC.Write(_descriptor, pointer, data.Length) != data.Length)
            {
                throw Failure("write to the monitor");
            }
        }
    }

    private unsafe void Read(Span<byte> buffer)
    {
        fixed (byte* pointer = buffer)
        {
            if (LibC.Read(_descriptor, pointer, buffer.Length) < 0)
            {
                throw Failure("read from the monitor");
            }
        }
    }

    private DdcException Failure(string operation)
    {
        var errno = Marshal.GetLastPInvokeError();
        return new DdcException(
            errno == LibC.ErrnoAccessDenied ? DdcError.AccessDenied : DdcError.NoResponse,
            $"Could not {operation} on {Path} (errno {errno}).");
    }
}
