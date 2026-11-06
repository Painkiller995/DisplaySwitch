namespace DisplaySwitch.Core.Monitors;

/// <summary>Enumerates the monitors that this operating system lets the application talk to.</summary>
public interface IDisplayBackend
{
    /// <summary>
    /// Enumerates attached monitors. The caller owns the returned monitors and must dispose them.
    /// Blocking; call off the UI thread.
    /// </summary>
    DisplayDiscovery Discover();
}

/// <summary>The outcome of monitor enumeration.</summary>
/// <param name="Monitors">Monitors found, including ones without DDC/CI support.</param>
/// <param name="Problem">
/// A platform-level reason why monitors may be missing or uncontrollable, written for end users
/// (for example a missing Linux permission), or <see langword="null"/> when there is none.
/// </param>
public sealed record DisplayDiscovery(IReadOnlyList<IDdcMonitor> Monitors, string? Problem = null);
