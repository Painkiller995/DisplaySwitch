using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DisplaySwitch.Core.Monitors;
using DisplaySwitch.Platform.Windows.Native;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.Platform.Windows;

/// <summary>Discovers monitors through the Win32 display APIs.</summary>
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsDisplayBackend(ILogger<WindowsDisplayBackend> logger) : IDisplayBackend
{
    private sealed record TargetDescription(string Name, string HardwareId, ConnectionKind Connection);

    private sealed record PhysicalMonitorEntry(nint Handle, TargetDescription? Target, int Width, int Height);

    public DisplayDiscovery Discover()
    {
        var targetsByGdiName = QueryTargetsByGdiName();
        var entries = new List<PhysicalMonitorEntry>();

        foreach (var monitorHandle in User32.EnumerateMonitorHandles())
        {
            var info = new User32.MonitorInfoEx { Size = (uint)Unsafe.SizeOf<User32.MonitorInfoEx>() };
            if (!User32.GetMonitorInfo(monitorHandle, ref info))
            {
                continue;
            }

            var physical = TryGetPhysicalMonitors(monitorHandle);
            if (physical is null)
            {
                continue;
            }

            var gdiName = ((ReadOnlySpan<char>)info.DeviceName).ToString().TrimEnd('\0');
            var targets = targetsByGdiName.GetValueOrDefault(gdiName) ?? [];
            for (var i = 0; i < physical.Length; i++)
            {
                // With cloned displays one HMONITOR has several targets. CCD and dxva2 are not
                // guaranteed to list them in the same order, so names are only trusted for a 1:1 match.
                var target = targets.Count == physical.Length ? targets[i] : null;
                entries.Add(new PhysicalMonitorEntry(
                    physical[i].Handle,
                    target,
                    info.Monitor.Right - info.Monitor.Left,
                    info.Monitor.Bottom - info.Monitor.Top));
            }
        }

        var keys = MonitorIdentity.AssignKeys([.. entries.Select(e => e.Target?.HardwareId ?? string.Empty)]);
        var monitors = entries.Select((entry, index) =>
        {
            var identity = new MonitorIdentity(
                keys[index],
                entry.Target?.HardwareId ?? string.Empty,
                entry.Target?.Name ?? $"Display {index + 1}",
                entry.Target?.Connection ?? ConnectionKind.Unknown,
                entry.Width,
                entry.Height);
            return (IDdcMonitor)new WindowsDdcMonitor(new SafePhysicalMonitorHandle(entry.Handle), identity);
        }).ToList();

        return new DisplayDiscovery(monitors);
    }

    private Dxva2.PhysicalMonitor[]? TryGetPhysicalMonitors(nint monitorHandle)
    {
        if (Dxva2.GetNumberOfPhysicalMonitorsFromHMONITOR(monitorHandle, out var count) && count > 0)
        {
            var physical = new Dxva2.PhysicalMonitor[count];
            if (Dxva2.GetPhysicalMonitorsFromHMONITOR(monitorHandle, count, physical))
            {
                return physical;
            }
        }

        var error = Marshal.GetLastPInvokeError();
        LogNoPhysicalMonitors(error);
        return null;
    }

    private Dictionary<string, List<TargetDescription>> QueryTargetsByGdiName()
    {
        var result = new Dictionary<string, List<TargetDescription>>(StringComparer.OrdinalIgnoreCase);
        if (!TryQueryActivePaths(out var paths))
        {
            return result;
        }

        foreach (var path in paths)
        {
            var source = new DisplayConfig.SourceDeviceName
            {
                Header = new DisplayConfig.DeviceInfoHeader
                {
                    Type = DisplayConfig.DeviceInfoGetSourceName,
                    Size = (uint)Unsafe.SizeOf<DisplayConfig.SourceDeviceName>(),
                    AdapterId = path.SourceAdapterId,
                    Id = path.SourceId,
                },
            };
            var target = new DisplayConfig.TargetDeviceName
            {
                Header = new DisplayConfig.DeviceInfoHeader
                {
                    Type = DisplayConfig.DeviceInfoGetTargetName,
                    Size = (uint)Unsafe.SizeOf<DisplayConfig.TargetDeviceName>(),
                    AdapterId = path.TargetAdapterId,
                    Id = path.TargetId,
                },
            };

            if (DisplayConfig.DisplayConfigGetDeviceInfo(ref source) != DisplayConfig.ErrorSuccess
                || DisplayConfig.DisplayConfigGetDeviceInfo(ref target) != DisplayConfig.ErrorSuccess)
            {
                continue;
            }

            var gdiName = ((ReadOnlySpan<char>)source.ViewGdiDeviceName).ToString().TrimEnd('\0');
            if (!result.TryGetValue(gdiName, out var list))
            {
                result[gdiName] = list = [];
            }

            list.Add(Describe(target));
        }

        return result;
    }

    private bool TryQueryActivePaths(out DisplayConfig.PathInfo[] paths)
    {
        // The topology can change between the size query and the data query; retry on that race.
        const int MaxAttempts = 3;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (DisplayConfig.GetDisplayConfigBufferSizes(DisplayConfig.QueryOnlyActivePaths, out var pathCount, out var modeCount) != DisplayConfig.ErrorSuccess)
            {
                break;
            }

            paths = new DisplayConfig.PathInfo[pathCount];
            var modes = new DisplayConfig.ModeInfo[modeCount];
            var status = DisplayConfig.QueryDisplayConfig(DisplayConfig.QueryOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, 0);
            if (status == DisplayConfig.ErrorSuccess)
            {
                paths = paths[..(int)pathCount];
                return true;
            }

            if (status != DisplayConfig.ErrorInsufficientBuffer)
            {
                LogDisplayConfigFailed(status);
                break;
            }
        }

        paths = [];
        return false;
    }

    private static TargetDescription Describe(DisplayConfig.TargetDeviceName target)
    {
        var connection = target.OutputTechnology switch
        {
            DisplayConfig.OutputTechnology.DisplayPortExternal => ConnectionKind.DisplayPort,
            DisplayConfig.OutputTechnology.Hdmi => ConnectionKind.Hdmi,
            DisplayConfig.OutputTechnology.Dvi => ConnectionKind.Dvi,
            DisplayConfig.OutputTechnology.Hd15 => ConnectionKind.Vga,
            DisplayConfig.OutputTechnology.Internal or DisplayConfig.OutputTechnology.DisplayPortEmbedded => ConnectionKind.Internal,
            DisplayConfig.OutputTechnology.Miracast => ConnectionKind.Wireless,
            _ => ConnectionKind.Unknown,
        };

        var hardwareId = (target.Flags & DisplayConfig.TargetNameFlagEdidIdsValid) != 0
            // CCD returns the EDID manufacturer bytes in their on-wire (big-endian) order inside a little-endian ushort.
            ? MonitorIdentity.FormatHardwareId(
                MonitorIdentity.DecodePnpId((ushort)((target.EdidManufactureId >> 8) | (target.EdidManufactureId << 8))),
                target.EdidProductCodeId)
            : string.Empty;

        var friendlyName = ((ReadOnlySpan<char>)target.MonitorFriendlyDeviceName).ToString().TrimEnd('\0').Trim();
        var name = friendlyName.Length > 0 ? friendlyName
            : connection == ConnectionKind.Internal ? "Built-in display"
            : "Display";

        return new TargetDescription(name, hardwareId, connection);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "A display has no DDC-capable physical monitor (Win32 error {Error})")]
    private partial void LogNoPhysicalMonitors(int error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "QueryDisplayConfig failed with Win32 error {Error}; monitor names will be generic")]
    private partial void LogDisplayConfigFailed(int error);
}
