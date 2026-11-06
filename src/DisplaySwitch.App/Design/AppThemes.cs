using Avalonia.Styling;

namespace DisplaySwitch.App.Design;

/// <summary>Theme variants beyond Avalonia's built-in Light and Dark.</summary>
public static class AppThemes
{
    /// <summary>
    /// Applied when the OS requests high contrast. Inherits Dark so Fluent control templates fall back
    /// to their dark resources for anything the token file does not override.
    /// </summary>
    public static ThemeVariant HighContrast { get; } = new("HighContrast", ThemeVariant.Dark);
}
