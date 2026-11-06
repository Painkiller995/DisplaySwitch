using System.Runtime.Versioning;
using DisplaySwitch.Platform.Windows.Native;

namespace DisplaySwitch.Platform.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowsDisplayPreferences
{
    /// <summary>Reflects Settings › Accessibility › Visual effects › Animation effects.</summary>
    public static bool PrefersReducedMotion() =>
        User32.SystemParametersInfo(User32.SpiGetClientAreaAnimation, 0, out var animate, 0) && !animate;
}
