using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Shortcuts;

namespace DisplaySwitch.Core.Settings;

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

/// <summary>The complete persisted user configuration. Immutable; change it through <see cref="ISettingsStore.Update"/>.</summary>
public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public ThemePreference Theme { get; init; }

    /// <summary>Turns off non-essential animation, in addition to the operating system preference.</summary>
    public bool ReduceMotion { get; init; }

    /// <summary>Closing the main window keeps the app running in the tray instead of exiting.</summary>
    public bool CloseToTray { get; init; } = true;

    public bool OnboardingCompleted { get; init; }

    public string? SelectedMonitorKey { get; init; }

    public IReadOnlyList<MonitorSettings> Monitors { get; init; } = [];

    public IReadOnlyList<ShortcutBinding> Shortcuts { get; init; } = [];

    public MonitorSettings? FindMonitor(string monitorKey) =>
        Monitors.FirstOrDefault(m => string.Equals(m.MonitorKey, monitorKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns a copy with the settings for one monitor replaced (or added).</summary>
    public AppSettings WithMonitor(string monitorKey, Func<MonitorSettings, MonitorSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var existing = FindMonitor(monitorKey) ?? new MonitorSettings { MonitorKey = monitorKey };
        var updated = update(existing);
        return this with { Monitors = [.. Monitors.Where(m => m != existing), updated] };
    }
}

/// <summary>Per-monitor preferences, keyed by <see cref="Monitors.MonitorIdentity.Key"/>.</summary>
public sealed record MonitorSettings
{
    public required string MonitorKey { get; init; }

    /// <summary>The user's own name for the monitor, replacing its model name in the UI.</summary>
    public string? CustomName { get; init; }

    /// <summary>
    /// The customized input list. <see langword="null"/> means "use the detected inputs", so
    /// improvements to detection still reach users who never customized anything.
    /// </summary>
    public IReadOnlyList<InputSource>? Inputs { get; init; }

    /// <summary>Raw capabilities string from the last successful read, to avoid a slow query at every start.</summary>
    public string? CachedCapabilities { get; init; }
}
