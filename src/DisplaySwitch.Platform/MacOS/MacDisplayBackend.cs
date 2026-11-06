using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DisplaySwitch.Core.Ddc;
using DisplaySwitch.Core.Monitors;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.Platform.MacOS;

/// <summary>Discovers external displays on Apple silicon Macs through IOAVService.</summary>
[SupportedOSPlatform("macos")]
internal sealed partial class MacDisplayBackend(ILogger<MacDisplayBackend> logger) : IDisplayBackend
{
    /// <summary>The IORegistry class that fronts each display port's AV service on Apple silicon.</summary>
    private const string AvServiceClass = "DCPAVServiceProxy";

    public DisplayDiscovery Discover()
    {
        if (RuntimeInformation.OSArchitecture != Architecture.Arm64)
        {
            return new DisplayDiscovery([], "Input switching on macOS currently requires a Mac with Apple silicon.");
        }

        var services = new List<nint>();
        if (IOKit.IOServiceGetMatchingServices(0, IOKit.IOServiceMatching(AvServiceClass), out var iterator) != IOKit.Success)
        {
            return new DisplayDiscovery([], "macOS did not allow DisplaySwitch to list displays.");
        }

        try
        {
            uint entry;
            while ((entry = IOKit.IOIteratorNext(iterator)) != 0)
            {
                try
                {
                    // Built-in panels expose the same service with Location "Embedded"; only external ones speak DDC/CI.
                    if (IOKit.ReadStringProperty(entry, "Location") == "External"
                        && IOKit.IOAVServiceCreateWithService(0, entry) is var service and not 0)
                    {
                        services.Add(service);
                    }
                }
                finally
                {
                    _ = IOKit.IOObjectRelease(entry);
                }
            }
        }
        finally
        {
            _ = IOKit.IOObjectRelease(iterator);
        }

        var transports = services.Select(s => new AvServiceTransport(s)).ToList();
        var edids = transports.Select(t => t.TryReadEdid() is { } edid && EdidParser.TryParse(edid, out var info) ? info : null).ToList();
        var keys = MonitorIdentity.AssignKeys([.. edids.Select(e => e?.HardwareId ?? string.Empty)]);
        var monitors = transports.Select((transport, i) => (IDdcMonitor)new RawDdcMonitor(
            new MonitorIdentity(keys[i], edids[i]?.HardwareId ?? string.Empty, edids[i]?.ModelName ?? $"External display {i + 1}", ConnectionKind.Unknown, 0, 0),
            transport)).ToList();

        LogDiscovered(monitors.Count);
        return new DisplayDiscovery(monitors);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} external display service(s)")]
    private partial void LogDiscovered(int count);

    /// <summary>Raw DDC/CI transport over one IOAVService handle.</summary>
    private sealed class AvServiceTransport(nint service) : IRawDdcTransport
    {
        private nint _service = service;

        public unsafe void Transfer(ReadOnlySpan<byte> request, Span<byte> reply)
        {
            // IOAVService takes the 0x51 source byte as a separate "data address" argument.
            var payload = request[1..];
            fixed (byte* pointer = payload)
            {
                Check(IOKit.IOAVServiceWriteI2C(_service, DdcProtocol.DdcCiAddress, request[0], pointer, (uint)payload.Length), "write to");
            }

            if (reply.IsEmpty)
            {
                return;
            }

            Thread.Sleep(DdcProtocol.ReplyDelay);
            fixed (byte* pointer = reply)
            {
                Check(IOKit.IOAVServiceReadI2C(_service, DdcProtocol.DdcCiAddress, request[0], pointer, (uint)reply.Length), "read from");
            }
        }

        public unsafe byte[]? TryReadEdid()
        {
            var edid = new byte[EdidParser.BlockLength];
            fixed (byte* pointer = edid)
            {
                return IOKit.IOAVServiceReadI2C(_service, DdcProtocol.EdidAddress, 0, pointer, (uint)edid.Length) == IOKit.Success ? edid : null;
            }
        }

        public void Dispose()
        {
            if (_service != 0)
            {
                IOKit.CFRelease(_service);
                _service = 0;
            }
        }

        private static void Check(int status, string operation)
        {
            if (status != IOKit.Success)
            {
                throw new DdcException(DdcError.NoResponse, $"Could not {operation} the monitor (IOReturn 0x{status:X8}).");
            }
        }
    }
}
