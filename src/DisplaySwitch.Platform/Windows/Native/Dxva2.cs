using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace DisplaySwitch.Platform.Windows.Native;

/// <summary>
/// The Windows Monitor Configuration API (dxva2.dll), which implements DDC/CI on top of the display driver.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class Dxva2
{
    private const string Library = "dxva2.dll";

    [LibraryImport(Library, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint monitor, out uint count);

    [LibraryImport(Library, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetPhysicalMonitorsFromHMONITOR(nint monitor, uint count, [Out] PhysicalMonitor[] monitors);

    [LibraryImport(Library, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyPhysicalMonitor(nint physicalMonitor);

    [LibraryImport(Library, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCapabilitiesStringLength(SafePhysicalMonitorHandle monitor, out uint length);

    [LibraryImport(Library, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CapabilitiesRequestAndCapabilitiesReply(SafePhysicalMonitorHandle monitor, [Out] byte[] buffer, uint length);

    [LibraryImport(Library, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetVCPFeatureAndVCPFeatureReply(SafePhysicalMonitorHandle monitor, byte vcpCode, out uint codeType, out uint currentValue, out uint maximumValue);

    [LibraryImport(Library, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetVCPFeature(SafePhysicalMonitorHandle monitor, byte vcpCode, uint newValue);

    [StructLayout(LayoutKind.Sequential)]
    public struct PhysicalMonitor
    {
        public nint Handle;
        public Description128 Description;
    }

    [InlineArray(128)]
    public struct Description128
    {
        private char _first;
    }
}

/// <summary>Owns a dxva2 physical-monitor handle and releases it with <c>DestroyPhysicalMonitor</c>.</summary>
[SupportedOSPlatform("windows")]
internal sealed class SafePhysicalMonitorHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafePhysicalMonitorHandle()
        : base(ownsHandle: true)
    {
    }

    public SafePhysicalMonitorHandle(nint handle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle() => Dxva2.DestroyPhysicalMonitor(handle);
}
