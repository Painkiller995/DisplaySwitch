using DisplaySwitch.Core.Settings;
using DisplaySwitch.Core.Switching;

namespace DisplaySwitch.App.Services;

/// <summary>Keeps capabilities strings in the settings file so later launches skip the slow DDC query.</summary>
internal sealed class SettingsCapabilitiesCache(ISettingsStore settings) : ICapabilitiesCache
{
    public string? Find(string monitorKey) => settings.Current.FindMonitor(monitorKey)?.CachedCapabilities;

    public void Store(string monitorKey, string capabilities) =>
        settings.Update(s => s.FindMonitor(monitorKey)?.CachedCapabilities == capabilities
            ? s
            : s.WithMonitor(monitorKey, m => m with { CachedCapabilities = capabilities }));
}
