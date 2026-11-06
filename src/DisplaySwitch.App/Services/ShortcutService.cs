using Avalonia.Threading;
using DisplaySwitch.App.ViewModels;
using DisplaySwitch.Core.Settings;
using DisplaySwitch.Core.Shortcuts;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.App.Services;

/// <summary>
/// Keeps the operating system's global shortcuts in sync with settings and runs the bound switch
/// when one is pressed.
/// </summary>
public sealed partial class ShortcutService : IDisposable
{
    private readonly IGlobalHotkeyService _hotkeys;
    private readonly ISettingsStore _settings;
    private readonly WorkspaceViewModel _workspace;
    private readonly ILogger _logger;
    private IReadOnlyList<HotkeyGesture> _registered = [];

    public ShortcutService(IGlobalHotkeyService hotkeys, ISettingsStore settings, WorkspaceViewModel workspace, ILogger<ShortcutService> logger)
    {
        _hotkeys = hotkeys;
        _settings = settings;
        _workspace = workspace;
        _logger = logger;
        _hotkeys.Pressed += OnPressed;
        _settings.Changed += (_, _) => _ = SyncAsync();
    }

    public bool IsSupported => _hotkeys.IsSupported;

    /// <summary>Shortcuts the OS refused, usually because another application owns them.</summary>
    public IReadOnlySet<HotkeyGesture> Unavailable { get; private set; } = new HashSet<HotkeyGesture>();

    /// <summary>Raised on the UI thread after a shortcut-triggered switch, with the feedback to show.</summary>
    public event EventHandler<Feedback>? SwitchCompleted;

    public bool IsTakenBySystem(HotkeyGesture gesture) => Unavailable.Contains(gesture);

    /// <summary>Registers the current shortcuts. Safe to call repeatedly; it only re-registers on change.</summary>
    public async Task SyncAsync()
    {
        if (!IsSupported)
        {
            return;
        }

        var gestures = _settings.Current.Shortcuts.Select(s => s.Gesture).Distinct().ToList();
        if (gestures.SequenceEqual(_registered))
        {
            return;
        }

        _registered = gestures;
        Unavailable = await _hotkeys.RegisterAsync(gestures);
        LogRegistered(gestures.Count, Unavailable.Count);
    }

    public void Dispose() => _hotkeys.Pressed -= OnPressed;

    private void OnPressed(object? sender, HotkeyGesture gesture) =>
        // Raised on the hotkey thread; all state lives on the UI thread.
        Dispatcher.UIThread.Post(async () =>
        {
            var binding = _settings.Current.Shortcuts.FirstOrDefault(s => s.Gesture == gesture);
            LogPressed(gesture, binding);
            if (binding is null)
            {
                return;
            }

            var outcome = await _workspace.SwitchByShortcutAsync(binding);
            if (outcome is { Feedback: { } feedback })
            {
                SwitchCompleted?.Invoke(this, feedback);
            }
        });

    [LoggerMessage(Level = LogLevel.Information, Message = "Shortcut {Gesture} pressed; binding: {Binding}")]
    private partial void LogPressed(HotkeyGesture gesture, ShortcutBinding? binding);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered {Count} global shortcut(s); {Unavailable} unavailable")]
    private partial void LogRegistered(int count, int unavailable);
}
