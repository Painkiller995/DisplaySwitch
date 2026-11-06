using DisplaySwitch.Core.Monitors;

namespace DisplaySwitch.TestSupport;

/// <summary>Returns a fixed set of simulated monitors.</summary>
public sealed class FakeDisplayBackend(params IDdcMonitor[] monitors) : IDisplayBackend
{
    public string? Problem { get; init; }

    public DisplayDiscovery Discover() => new(monitors, Problem);
}
