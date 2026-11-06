using DisplaySwitch.Core.Ddc;

namespace DisplaySwitch.Core.Monitors;

/// <summary>
/// A platform-specific DDC/CI connection to one physical monitor.
/// </summary>
/// <remarks>
/// Implementations are synchronous and blocking because every underlying OS API is. They are not
/// thread-safe: DDC/CI cannot interleave requests on one bus, so callers must serialize access
/// (see <see cref="Switching.MonitorChannel"/>). Failures are reported as <see cref="DdcException"/>.
/// </remarks>
public interface IDdcMonitor : IDisposable
{
    MonitorIdentity Identity { get; }

    /// <summary>Whether DDC/CI is worth attempting at all (built-in laptop panels never support it).</summary>
    bool SupportsDdc { get; }

    /// <summary>Requests the MCCS capabilities string. Slow: typically 0.3–3 seconds.</summary>
    string ReadCapabilities();

    VcpReading ReadVcp(byte vcpCode);

    void WriteVcp(byte vcpCode, ushort value);
}
