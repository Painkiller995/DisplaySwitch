using DisplaySwitch.Core.Inputs;

namespace DisplaySwitch.Core.Settings;

/// <summary>Combines detected inputs with the user's customization into the list the UI shows.</summary>
public static class InputLayout
{
    /// <summary>All configured inputs, hidden ones included. This is also the set of codes that may be written.</summary>
    public static IReadOnlyList<InputSource> Resolve(IReadOnlyList<InputSource> detected, MonitorSettings? settings) =>
        settings?.Inputs ?? detected;

    /// <summary>The inputs offered for one-click switching.</summary>
    public static IReadOnlyList<InputSource> Visible(IReadOnlyList<InputSource> detected, MonitorSettings? settings) =>
        [.. Resolve(detected, settings).Where(i => !i.IsHidden)];
}
