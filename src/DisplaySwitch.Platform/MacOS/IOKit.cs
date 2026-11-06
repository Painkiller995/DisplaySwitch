using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DisplaySwitch.Platform.MacOS;

/// <summary>
/// IOKit and CoreFoundation entry points for DDC/CI on Apple silicon.
/// </summary>
/// <remarks>
/// IOAVService is the private API the macOS display stack uses to reach external displays' I²C bus.
/// It has no public replacement. The same calls are used by established tools such as m1ddc and
/// MonitorControl, but Apple may change them in any macOS release, so every failure is handled
/// as "monitor not controllable" and never as a crash.
/// </remarks>
[SupportedOSPlatform("macos")]
internal static partial class IOKit
{
    private const string IOKitLibrary = "/System/Library/Frameworks/IOKit.framework/IOKit";
    private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    public const int Success = 0;
    public const uint Utf8Encoding = 0x08000100;

    [LibraryImport(IOKitLibrary, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint IOServiceMatching(string serviceName);

    [LibraryImport(IOKitLibrary)]
    public static partial int IOServiceGetMatchingServices(uint mainPort, nint matching, out uint iterator);

    [LibraryImport(IOKitLibrary)]
    public static partial uint IOIteratorNext(uint iterator);

    [LibraryImport(IOKitLibrary)]
    public static partial int IOObjectRelease(uint ioObject);

    [LibraryImport(IOKitLibrary)]
    public static partial nint IORegistryEntryCreateCFProperty(uint entry, nint key, nint allocator, uint options);

    [LibraryImport(IOKitLibrary)]
    public static partial nint IOAVServiceCreateWithService(nint allocator, uint service);

    [LibraryImport(IOKitLibrary)]
    public static unsafe partial int IOAVServiceReadI2C(nint service, uint chipAddress, uint offset, byte* buffer, uint length);

    [LibraryImport(IOKitLibrary)]
    public static unsafe partial int IOAVServiceWriteI2C(nint service, uint chipAddress, uint dataAddress, byte* buffer, uint length);

    [LibraryImport(CoreFoundationLibrary, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint CFStringCreateWithCString(nint allocator, string text, uint encoding);

    [LibraryImport(CoreFoundationLibrary)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool CFStringGetCString(nint text, byte* buffer, nint bufferSize, uint encoding);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nuint CFGetTypeID(nint value);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nuint CFStringGetTypeID();

    [LibraryImport(CoreFoundationLibrary)]
    public static partial void CFRelease(nint value);

    /// <summary>Reads a string property of an IORegistry entry, or returns <see langword="null"/>.</summary>
    public static unsafe string? ReadStringProperty(uint entry, string key)
    {
        var cfKey = CFStringCreateWithCString(0, key, Utf8Encoding);
        if (cfKey == 0)
        {
            return null;
        }

        try
        {
            var value = IORegistryEntryCreateCFProperty(entry, cfKey, 0, 0);
            if (value == 0)
            {
                return null;
            }

            try
            {
                if (CFGetTypeID(value) != CFStringGetTypeID())
                {
                    return null;
                }

                const int BufferSize = 256;
                var buffer = stackalloc byte[BufferSize];
                return CFStringGetCString(value, buffer, BufferSize, Utf8Encoding) ? Marshal.PtrToStringUTF8((nint)buffer) : null;
            }
            finally
            {
                CFRelease(value);
            }
        }
        finally
        {
            CFRelease(cfKey);
        }
    }
}
