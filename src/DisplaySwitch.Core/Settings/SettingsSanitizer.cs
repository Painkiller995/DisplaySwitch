using DisplaySwitch.Core.Inputs;

namespace DisplaySwitch.Core.Settings;

/// <summary>
/// Repairs settings loaded from disk. The file is user-editable, so it is treated as untrusted input:
/// invalid entries are dropped or clamped instead of failing the whole load.
/// </summary>
public static class SettingsSanitizer
{
    private const int MaxMonitorNameLength = 60;
    private const int MaxCapabilitiesLength = 4096;

    public static AppSettings Sanitize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings with
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            Theme = Enum.IsDefined(settings.Theme) ? settings.Theme : ThemePreference.System,
            Monitors =
            [
                .. (settings.Monitors ?? [])
                    .Where(m => m is not null && !string.IsNullOrWhiteSpace(m.MonitorKey))
                    .DistinctBy(m => m.MonitorKey, StringComparer.OrdinalIgnoreCase)
                    .Select(SanitizeMonitor),
            ],
            Shortcuts =
            [
                .. (settings.Shortcuts ?? [])
                    .Where(s => s is not null && s.Gesture.IsValid && s.Input.IsValid && !string.IsNullOrWhiteSpace(s.MonitorKey))
                    .DistinctBy(s => s.Gesture),
            ],
        };
    }

    private static MonitorSettings SanitizeMonitor(MonitorSettings monitor) => monitor with
    {
        CustomName = Clamp(monitor.CustomName, MaxMonitorNameLength),
        CachedCapabilities = monitor.CachedCapabilities?.Length > MaxCapabilitiesLength ? null : monitor.CachedCapabilities,
        Inputs = monitor.Inputs is null
            ? null
            : [.. monitor.Inputs.Where(i => i is not null && i.Code.IsValid).DistinctBy(i => i.Code).Select(SanitizeInput)],
    };

    private static InputSource SanitizeInput(InputSource input) => input with
    {
        PortName = Clamp(input.PortName, InputSource.MaxLabelLength) ?? $"Input {input.Code}",
        Label = Clamp(input.Label, InputSource.MaxLabelLength),
        Kind = Enum.IsDefined(input.Kind) ? input.Kind : InputKind.Other,
        Icon = Enum.IsDefined(input.Icon) ? input.Icon : InputIcon.Port,
        ReadAliases = [.. (input.ReadAliases ?? []).Where(a => a.IsValid && a != input.Code).Distinct()],
    };

    private static string? Clamp(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
