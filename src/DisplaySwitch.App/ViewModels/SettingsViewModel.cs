using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplaySwitch.App.Services;
using DisplaySwitch.Core.Platform;
using DisplaySwitch.Core.Settings;

namespace DisplaySwitch.App.ViewModels;

/// <summary>Application preferences. Every change is saved immediately; there is no Apply button.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly IAutostartService _autostart;
    private readonly ShortcutService _shortcuts;

    public SettingsViewModel(ISettingsStore settings, IAutostartService autostart, ShortcutService shortcuts, WorkspaceViewModel workspace, AppPaths paths)
    {
        _settings = settings;
        _autostart = autostart;
        _shortcuts = shortcuts;
        Workspace = workspace;
        LogDirectory = paths.LogDirectory;
        _startAtSignIn = autostart.IsSupported && autostart.IsEnabled;
        _settings.Changed += (_, _) => OnPropertyChanged(string.Empty);
    }

    public WorkspaceViewModel Workspace { get; }

    public string LogDirectory { get; }

    public string Version { get; } =
        typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "1.0.0";

    public bool IsSystemTheme
    {
        get => _settings.Current.Theme == ThemePreference.System;
        set => SetTheme(value, ThemePreference.System);
    }

    public bool IsLightTheme
    {
        get => _settings.Current.Theme == ThemePreference.Light;
        set => SetTheme(value, ThemePreference.Light);
    }

    public bool IsDarkTheme
    {
        get => _settings.Current.Theme == ThemePreference.Dark;
        set => SetTheme(value, ThemePreference.Dark);
    }

    public bool ReduceMotion
    {
        get => _settings.Current.ReduceMotion;
        set => _settings.Update(s => s with { ReduceMotion = value });
    }

    public bool CloseToTray
    {
        get => _settings.Current.CloseToTray;
        set => _settings.Update(s => s with { CloseToTray = value });
    }

    public bool AutostartSupported => _autostart.IsSupported;

    private bool _startAtSignIn;

    public bool StartAtSignIn
    {
        get => _startAtSignIn;
        set
        {
            try
            {
                _autostart.SetEnabled(value);
                AutostartError = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AutostartError = $"Couldn't change the startup setting: {ex.Message}";
            }

            // Re-read from the OS, which owns the truth.
            SetProperty(ref _startAtSignIn, _autostart.IsEnabled);
        }
    }

    [ObservableProperty]
    public partial string? AutostartError { get; private set; }

    public string ShortcutsSummary
    {
        get
        {
            if (!_shortcuts.IsSupported)
            {
                return OperatingSystem.IsMacOS()
                    ? "Global shortcuts aren't available on macOS yet. Use the menu bar icon to switch quickly."
                    : "Global shortcuts aren't available on Linux yet. Use the tray icon to switch quickly.";
            }

            var count = _settings.Current.Shortcuts.Count;
            var unavailable = _settings.Current.Shortcuts.Count(s => _shortcuts.IsTakenBySystem(s.Gesture));
            var summary = count switch
            {
                0 => "No shortcuts yet. Open an input's menu to assign one.",
                1 => "1 shortcut assigned.",
                _ => $"{count} shortcuts assigned.",
            };
            return unavailable == 0 ? summary : $"{summary} {unavailable} can't be used because another app owns it.";
        }
    }

    [RelayCommand]
    private Task RescanAsync() => Workspace.RefreshAsync(forceCapabilities: true);

    /// <summary>Discards customization for the selected monitor and returns to the detected inputs.</summary>
    [RelayCommand]
    private void ResetInputs()
    {
        if (Workspace.SelectedMonitor is not { } monitor)
        {
            return;
        }

        var key = monitor.Key;
        _settings.Update(s => s.WithMonitor(key, m => m with { Inputs = null, CustomName = null }) with
        {
            Shortcuts = [.. s.Shortcuts.Where(b => !string.Equals(b.MonitorKey, key, StringComparison.OrdinalIgnoreCase))],
        });
    }

    public string? SelectedCapabilities =>
        Workspace.SelectedMonitor is { } monitor ? _settings.Current.FindMonitor(monitor.Key)?.CachedCapabilities ?? "Not available" : null;

    private void SetTheme(bool selected, ThemePreference theme)
    {
        if (selected)
        {
            _settings.Update(s => s with { Theme = theme });
        }
    }
}
