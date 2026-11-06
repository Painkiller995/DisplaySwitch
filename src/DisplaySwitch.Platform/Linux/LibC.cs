using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace DisplaySwitch.Platform.Linux;

[SupportedOSPlatform("linux")]
internal static partial class LibC
{
    private const string Library = "libc";

    public const int ReadWrite = 2;
    public const int ErrnoAccessDenied = 13;

    /// <summary>i2c-dev ioctl that selects the 7-bit slave address for subsequent reads and writes.</summary>
    public const nuint I2cSlave = 0x0703;

    [LibraryImport(Library, EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int Open(string path, int flags);

    [LibraryImport(Library, EntryPoint = "close", SetLastError = true)]
    public static partial int Close(int descriptor);

    [LibraryImport(Library, EntryPoint = "ioctl", SetLastError = true)]
    public static partial int IoControl(SafeFileDescriptor descriptor, nuint request, nint argument);

    [LibraryImport(Library, EntryPoint = "read", SetLastError = true)]
    public static unsafe partial nint Read(SafeFileDescriptor descriptor, byte* buffer, nint count);

    [LibraryImport(Library, EntryPoint = "write", SetLastError = true)]
    public static unsafe partial nint Write(SafeFileDescriptor descriptor, byte* buffer, nint count);
}

/// <summary>Owns a POSIX file descriptor.</summary>
[SupportedOSPlatform("linux")]
internal sealed class SafeFileDescriptor : SafeHandleMinusOneIsInvalid
{
    public SafeFileDescriptor()
        : base(ownsHandle: true)
    {
    }

    public SafeFileDescriptor(int descriptor)
        : base(ownsHandle: true)
    {
        SetHandle(descriptor);
    }

    protected override bool ReleaseHandle() => LibC.Close((int)handle) == 0;
}
