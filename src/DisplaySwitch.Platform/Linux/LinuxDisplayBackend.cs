using System.Globalization;
using System.Runtime.Versioning;
using DisplaySwitch.Core.Monitors;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.Platform.Linux;

/// <summary>
/// Discovers monitors through the kernel DRM subsystem (<c>/sys/class/drm</c>) and talks DDC/CI over
/// <c>/dev/i2c-N</c>, the same mechanism ddcutil uses, without depending on any external tool.
/// </summary>
/// <remarks>
/// Requires the <c>i2c-dev</c> kernel module and read/write access to the i2c device nodes, which
/// most distributions grant to the <c>i2c</c> group.
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed partial class LinuxDisplayBackend(ILogger<LinuxDisplayBackend> logger) : IDisplayBackend
{
    private const string DrmRoot = "/sys/class/drm";
    private const string DevRoot = "/dev";

    private sealed record Connector(string Name, ConnectionKind Connection, byte[] Edid, string? BusName, int Width, int Height);

    public DisplayDiscovery Discover()
    {
        var connectors = ReadConnectors();
        if (connectors.Count == 0)
        {
            return new DisplayDiscovery([], "No connected monitors were reported by the graphics driver.");
        }

        var edids = connectors.Select(c => EdidParser.TryParse(c.Edid, out var info) ? info : null).ToList();
        var keys = MonitorIdentity.AssignKeys([.. edids.Select(e => e?.HardwareId ?? string.Empty)]);
        var monitors = new List<IDdcMonitor>();
        string? problem = null;

        for (var i = 0; i < connectors.Count; i++)
        {
            var connector = connectors[i];
            var identity = new MonitorIdentity(
                keys[i],
                edids[i]?.HardwareId ?? string.Empty,
                edids[i]?.ModelName ?? connector.Name,
                connector.Connection,
                connector.Width,
                connector.Height);

            I2cBus? bus = null;
            if (connector.BusName is not null && connector.Connection != ConnectionKind.Internal)
            {
                (bus, var busProblem) = OpenBus(Path.Combine(DevRoot, connector.BusName));
                problem ??= busProblem;
            }

            monitors.Add(new RawDdcMonitor(identity, bus));
        }

        LogDiscovered(monitors.Count, problem);
        return new DisplayDiscovery(monitors, problem);
    }

    private static (I2cBus? Bus, string? Problem) OpenBus(string devicePath)
    {
        if (!File.Exists(devicePath))
        {
            return (null, "Monitor control needs the i2c-dev kernel module. Run “sudo modprobe i2c-dev”, then refresh.");
        }

        var bus = I2cBus.TryOpen(devicePath, out var errno);
        return bus is null && errno == LibC.ErrnoAccessDenied
            ? (null, "DisplaySwitch has no permission to access monitor controls. Add your user to the “i2c” group, then sign out and back in.")
            : (bus, null);
    }

    private static List<Connector> ReadConnectors()
    {
        if (!Directory.Exists(DrmRoot))
        {
            return [];
        }

        var connectors = new List<Connector>();
        foreach (var directory in Directory.EnumerateDirectories(DrmRoot, "card*-*").Order(StringComparer.Ordinal))
        {
            var statusFile = Path.Combine(directory, "status");
            var edidFile = Path.Combine(directory, "edid");
            if (!File.Exists(statusFile) || File.ReadAllText(statusFile).Trim() != "connected" || !File.Exists(edidFile))
            {
                continue;
            }

            var edid = File.ReadAllBytes(edidFile);
            if (edid.Length == 0)
            {
                continue;
            }

            // "card0-HDMI-A-1" → "HDMI-A-1"
            var name = Path.GetFileName(directory);
            name = name[(name.IndexOf('-', StringComparison.Ordinal) + 1)..];
            var (width, height) = ReadPreferredMode(directory);
            connectors.Add(new Connector(name, ClassifyConnector(name), edid, FindBusName(directory), width, height));
        }

        return connectors;
    }

    /// <summary>
    /// Finds the i2c adapter for a connector: the "ddc" link for HDMI/DVI/VGA, or the DisplayPort AUX
    /// channel's "i2c-N" child directory.
    /// </summary>
    private static string? FindBusName(string connectorDirectory)
    {
        var ddcLink = new DirectoryInfo(Path.Combine(connectorDirectory, "ddc"));
        if (ddcLink.Exists && ddcLink.ResolveLinkTarget(returnFinalTarget: true) is { } target)
        {
            return target.Name;
        }

        return Directory.EnumerateDirectories(connectorDirectory, "i2c-*").Select(Path.GetFileName).FirstOrDefault();
    }

    private static (int Width, int Height) ReadPreferredMode(string connectorDirectory)
    {
        var modesFile = Path.Combine(connectorDirectory, "modes");
        var first = File.Exists(modesFile) ? File.ReadLines(modesFile).FirstOrDefault() : null;
        var parts = first?.Split('x');
        return parts is [var w, var h]
            && int.TryParse(w, NumberStyles.None, CultureInfo.InvariantCulture, out var width)
            && int.TryParse(h.TrimEnd('i'), NumberStyles.None, CultureInfo.InvariantCulture, out var height)
            ? (width, height)
            : (0, 0);
    }

    private static ConnectionKind ClassifyConnector(string name) => name switch
    {
        _ when name.StartsWith("eDP", StringComparison.Ordinal) || name.StartsWith("LVDS", StringComparison.Ordinal) || name.StartsWith("DSI", StringComparison.Ordinal) => ConnectionKind.Internal,
        _ when name.StartsWith("DP", StringComparison.Ordinal) => ConnectionKind.DisplayPort,
        _ when name.StartsWith("HDMI", StringComparison.Ordinal) => ConnectionKind.Hdmi,
        _ when name.StartsWith("DVI", StringComparison.Ordinal) => ConnectionKind.Dvi,
        _ when name.StartsWith("VGA", StringComparison.Ordinal) => ConnectionKind.Vga,
        _ => ConnectionKind.Unknown,
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} DRM connector(s); access problem: {Problem}")]
    private partial void LogDiscovered(int count, string? problem);
}
