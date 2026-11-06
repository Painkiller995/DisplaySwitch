using DisplaySwitch.Core.Ddc;
using DisplaySwitch.Core.Monitors;

namespace DisplaySwitch.TestSupport;

/// <summary>Scriptable in-memory monitor for tests.</summary>
public sealed class FakeDdcMonitor : IDdcMonitor
{
    private readonly Lock _lock = new();
    private int _concurrentCalls;

    public FakeDdcMonitor(byte currentInput = 0x0F, string key = "TST0001", MonitorIdentity? identity = null)
    {
        CurrentInput = currentInput;
        Identity = identity ?? new MonitorIdentity(key, key, "Test Monitor", ConnectionKind.DisplayPort, 3840, 1080);
    }

    /// <summary>
    /// Simulated Samsung C49J89x: firmware declares 0x03 for DisplayPort and 0x01 for HDMI; brightness
    /// 0–100; color temperature request 0–170 without a reported step, and writes to it are ignored.
    /// </summary>
    public static FakeDdcMonitor SamsungC49J89x()
    {
        var monitor = new FakeDdcMonitor(0x03, identity: new MonitorIdentity("SAM0F21", "SAM0F21", "C49J89x", ConnectionKind.DisplayPort, 3840, 1080));
        monitor.Features[VcpCodes.Brightness] = (60, 100);
        monitor.Features[VcpCodes.ColorTemperatureRequest] = (40, 170);
        monitor.IgnoredFeatureWrites.Add(VcpCodes.ColorTemperatureRequest);
        return monitor;
    }

    public MonitorIdentity Identity { get; }

    public bool SupportsDdc { get; init; } = true;

    public byte CurrentInput { get; set; }

    /// <summary>Maps a written code to the value the monitor then reports; identity when absent.</summary>
    public Dictionary<byte, byte> ReportedAfterWrite { get; } = [];

    /// <summary>Codes the monitor accepts but ignores.</summary>
    public HashSet<byte> IgnoredCodes { get; } = [];

    /// <summary>Errors thrown by the next calls, consumed in order.</summary>
    public Queue<DdcError> UpcomingFailures { get; } = new();

    public bool StopRespondingAfterWrite { get; set; }

    public bool RejectWrites { get; set; }

    public TimeSpan CallDuration { get; set; }

    public string? Capabilities { get; set; }

    /// <summary>Input codes written to VCP 0x60.</summary>
    public List<byte> Writes { get; } = [];

    /// <summary>Continuous features other than input select, by VCP code. Absent codes are unsupported.</summary>
    public Dictionary<byte, (ushort Current, ushort Maximum)> Features { get; } = [];

    /// <summary>Features whose writes are accepted but have no effect.</summary>
    public HashSet<byte> IgnoredFeatureWrites { get; } = [];

    /// <summary>Writes to features other than input select, in order.</summary>
    public List<(byte Code, ushort Value)> FeatureWrites { get; } = [];

    public int MaxObservedConcurrency { get; private set; }

    private bool _silent;

    public string ReadCapabilities() => Invoke(() => Capabilities ?? throw new DdcException(DdcError.Unsupported, "no capabilities"));

    public VcpReading ReadVcp(byte vcpCode) => Invoke(() =>
    {
        if (_silent)
        {
            throw new DdcException(DdcError.NoResponse, "silent");
        }

        if (vcpCode == VcpCodes.InputSelect)
        {
            return new VcpReading(CurrentInput, CurrentInput);
        }

        lock (_lock)
        {
            return Features.TryGetValue(vcpCode, out var feature)
                ? new VcpReading(feature.Current, feature.Maximum)
                : throw new DdcException(DdcError.Unsupported, $"VCP 0x{vcpCode:X2} unsupported");
        }
    });

    public void WriteVcp(byte vcpCode, ushort value) => Invoke(() =>
    {
        if (RejectWrites)
        {
            throw new DdcException(DdcError.Unsupported, "rejected");
        }

        if (vcpCode != VcpCodes.InputSelect)
        {
            lock (_lock)
            {
                if (!Features.TryGetValue(vcpCode, out var feature))
                {
                    throw new DdcException(DdcError.Unsupported, $"VCP 0x{vcpCode:X2} unsupported");
                }

                FeatureWrites.Add((vcpCode, value));
                if (!IgnoredFeatureWrites.Contains(vcpCode))
                {
                    Features[vcpCode] = (value, feature.Maximum);
                }
            }

            return 0;
        }

        var code = (byte)value;
        Writes.Add(code);
        if (!IgnoredCodes.Contains(code))
        {
            CurrentInput = ReportedAfterWrite.GetValueOrDefault(code, code);
        }

        _silent = StopRespondingAfterWrite;
        return 0;
    });

    public void Dispose()
    {
    }

    private T Invoke<T>(Func<T> action)
    {
        lock (_lock)
        {
            MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, ++_concurrentCalls);
        }

        try
        {
            if (CallDuration > TimeSpan.Zero)
            {
                Thread.Sleep(CallDuration);
            }

            lock (_lock)
            {
                if (UpcomingFailures.TryDequeue(out var failure))
                {
                    throw new DdcException(failure, $"scripted {failure}");
                }
            }

            return action();
        }
        finally
        {
            lock (_lock)
            {
                _concurrentCalls--;
            }
        }
    }
}
