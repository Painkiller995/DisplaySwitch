using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Styling;
using DisplaySwitch.App.Design;
using DisplaySwitch.Core.Settings;

namespace DisplaySwitch.App.Services;

/// <summary>
/// Applies the theme (light, dark, system or high contrast) and the motion preference, and keeps
/// them in sync with the operating system and the user's settings.
/// </summary>
public sealed class AppearanceService
{
    private const string AnimatedClass = "animated";

    private readonly Application _application;
    private readonly ISettingsStore _settings;
    private readonly Func<bool> _systemPrefersReducedMotion;
    private readonly List<WeakReference<TopLevel>> _windows = [];

    public AppearanceService(Application application, ISettingsStore settings, Func<bool> systemPrefersReducedMotion)
    {
        _application = application;
        _settings = settings;
        _systemPrefersReducedMotion = systemPrefersReducedMotion;

        _settings.Changed += (_, _) => Apply();
        if (_application.PlatformSettings is { } platform)
        {
            platform.ColorValuesChanged += (_, _) => Apply();
        }
    }

    public bool AnimationsEnabled { get; private set; }

    /// <summary>Applies the motion preference to a window now and whenever it changes.</summary>
    public void Attach(TopLevel window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _windows.Add(new WeakReference<TopLevel>(window));
        window.Classes.Set(AnimatedClass, AnimationsEnabled);
    }

    public void Apply()
    {
        var highContrast = _application.PlatformSettings?.GetColorValues().ContrastPreference == ColorContrastPreference.High;
        _application.RequestedThemeVariant = highContrast
            ? AppThemes.HighContrast
            : _settings.Current.Theme switch
            {
                ThemePreference.Light => ThemeVariant.Light,
                ThemePreference.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };

        AnimationsEnabled = !_settings.Current.ReduceMotion && !_systemPrefersReducedMotion();
        _windows.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in _windows)
        {
            if (reference.TryGetTarget(out var window))
            {
                window.Classes.Set(AnimatedClass, AnimationsEnabled);
            }
        }
    }
}
