using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DisplaySwitch.Platform.Windows.Native;

[SupportedOSPlatform("windows")]
internal static partial class User32
{
    private const string Library = "user32.dll";

    public const uint WmHotkey = 0x0312;
    public const uint WmApp = 0x8000;
    public const uint WmQuit = 0x0012;
    public const uint PmNoRemove = 0x0000;
    public const uint ModAlt = 0x1;
    public const uint ModControl = 0x2;
    public const uint ModShift = 0x4;
    public const uint ModWin = 0x8;

    /// <summary>Stops auto-repeat from firing the hotkey repeatedly while it is held down.</summary>
    public const uint ModNoRepeat = 0x4000;

    public const uint SpiGetClientAreaAnimation = 0x1042;

    [LibraryImport(Library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool EnumDisplayMonitors(nint hdc, nint clip, delegate* unmanaged<nint, nint, nint, nint, int> callback, nint data);

    /// <summary>Returns the HMONITOR of every display in the virtual desktop.</summary>
    public static unsafe List<nint> EnumerateMonitorHandles()
    {
        var handles = new List<nint>();
        var state = GCHandle.Alloc(handles);
        try
        {
            EnumDisplayMonitors(0, 0, &CollectMonitor, GCHandle.ToIntPtr(state));
        }
        finally
        {
            state.Free();
        }

        return handles;
    }

    [UnmanagedCallersOnly]
    private static int CollectMonitor(nint monitor, nint hdc, nint rect, nint data)
    {
        ((List<nint>)GCHandle.FromIntPtr(data).Target!).Add(monitor);
        return 1;
    }

    [LibraryImport(Library, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(nint monitor, ref MonitorInfoEx info);

    [LibraryImport(Library, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [LibraryImport(Library, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint window, int id);

    [LibraryImport(Library, EntryPoint = "GetMessageW")]
    public static partial int GetMessage(out Msg message, nint window, uint filterMin, uint filterMax);

    [LibraryImport(Library, EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PeekMessage(out Msg message, nint window, uint filterMin, uint filterMax, uint remove);

    [LibraryImport(Library, EntryPoint = "PostThreadMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam);

    [LibraryImport(Library, EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfoEx
    {
        public uint Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
        public DeviceName32 DeviceName;
    }

    [InlineArray(32)]
    public struct DeviceName32
    {
        private char _first;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Msg
    {
        public nint Window;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int PointX;
        public int PointY;
        public uint Private;
    }
}

[SupportedOSPlatform("windows")]
internal static partial class Kernel32
{
    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();
}
