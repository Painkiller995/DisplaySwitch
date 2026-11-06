using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DisplaySwitch.Platform.Windows.Native;

/// <summary>
/// The Connecting and Configuring Displays (CCD) API. dxva2 only describes monitors as
/// "Generic PnP Monitor", so CCD supplies the real model name, EDID IDs and connector type.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class DisplayConfig
{
    private const string Library = "user32.dll";

    public const uint QueryOnlyActivePaths = 0x2;
    public const int ErrorSuccess = 0;
    public const int ErrorInsufficientBuffer = 122;
    public const int DeviceInfoGetSourceName = 1;
    public const int DeviceInfoGetTargetName = 2;
    public const uint TargetNameFlagEdidIdsValid = 0x4;

    [LibraryImport(Library)]
    public static partial int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [LibraryImport(Library)]
    public static partial int QueryDisplayConfig(uint flags, ref uint pathCount, [Out] PathInfo[] paths, ref uint modeCount, [Out] ModeInfo[] modes, nint currentTopologyId);

    [LibraryImport(Library)]
    public static partial int DisplayConfigGetDeviceInfo(ref SourceDeviceName request);

    [LibraryImport(Library)]
    public static partial int DisplayConfigGetDeviceInfo(ref TargetDeviceName request);

    /// <summary>DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY values that matter for user-facing connection labels.</summary>
    public enum OutputTechnology : uint
    {
        Hd15 = 0,
        Dvi = 4,
        Hdmi = 5,
        DisplayPortExternal = 10,
        DisplayPortEmbedded = 11,
        Miracast = 15,
        Internal = 0x80000000,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PathInfo
    {
        public Luid SourceAdapterId;
        public uint SourceId;
        public uint SourceModeInfoIndex;
        public uint SourceStatusFlags;
        public Luid TargetAdapterId;
        public uint TargetId;
        public uint TargetModeInfoIndex;
        public OutputTechnology OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public uint RefreshRateNumerator;
        public uint RefreshRateDenominator;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint TargetStatusFlags;
        public uint Flags;
    }

    /// <summary>DISPLAYCONFIG_MODE_INFO. Only allocated for QueryDisplayConfig; its contents are not read.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ModeInfo
    {
        public uint InfoType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceInfoHeader
    {
        public int Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SourceDeviceName
    {
        public DeviceInfoHeader Header;
        public Chars32 ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TargetDeviceName
    {
        public DeviceInfoHeader Header;
        public uint Flags;
        public OutputTechnology OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;
        public Chars64 MonitorFriendlyDeviceName;
        public Chars128 MonitorDevicePath;
    }

    [InlineArray(32)]
    public struct Chars32
    {
        private char _first;
    }

    [InlineArray(64)]
    public struct Chars64
    {
        private char _first;
    }

    [InlineArray(128)]
    public struct Chars128
    {
        private char _first;
    }
}
