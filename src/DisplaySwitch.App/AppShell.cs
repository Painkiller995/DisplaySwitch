using Avalonia;
using Avalonia.Threading;
using DisplaySwitch.App.Services;
using DisplaySwitch.App.ViewModels;
using DisplaySwitch.App.Views;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.App;

/// <summary>
/// Owns the application's windows, tray icon and view models, and their lifetimes.
/// </summary>
internal sealed class AppShell : IDisposable
{
    /// <summary>Re-reading active inputs on every focus change would flood the DDC bus; once per interval is plenty.</summary>
    private static readonly TimeSpan ActiveInputRefreshInterval = TimeSpan.FromSeconds(3);

    /// <summary>Display topology changes arrive in bursts (one per monitor and mode change).</summary>
    private static readonly TimeSpan DisplayChangeDebounce = TimeSpan.FromSeconds(1.5);

    private readonly AppServices _services;
    private readonly Action _shutdown;
    private readonly AppearanceService _appearance;
    private readonly ShortcutService _shortcuts;
    private readonly TrayController? _tray;
    private QuickSwitchWindow? _quickSwitch;
    private ToastWindow? _toast;
    private MainWindow? _mainWindow;
    private DateTimeOffset _lastActiveRefresh = DateTimeOffset.MinValue;
    private DispatcherTimer? _displayChangeTimer;

    public AppShell(Application application, AppServices services, bool enableTray, Action shutdown)
    {
        _services = services;
        _shutdown = shutdown;

        _appearance = new AppearanceService(application, services.Settings, () => services.Platform.PrefersReducedMotion);
        _appearance.Apply();

        Workspace = new WorkspaceViewModel(services.Directory, services.Settings);
        _shortcuts = new ShortcutService(services.Platform.Hotkeys, services.Settings, Workspace, services.LoggerFactory.CreateLogger<ShortcutService>());
        _shortcuts.SwitchCompleted += (_, feedback) => ShowToastIfBackground(feedback);

        var settingsPage = new SettingsViewModel(services.Settings, services.Platform.Autostart, _shortcuts, Workspace, services.Paths);
        Main = new MainViewModel(Workspace, settingsPage, services.Settings, _shortcuts);

        if (enableTray)
        {
            _tray = new TrayController(application, Workspace, ToggleQuickSwitch, ShowMainWindow, OpenSettings, Quit);
        }
    }

    public WorkspaceViewModel Workspace { get; }

    public MainViewModel Main { get; }

    public MainWindow MainWindow => _mainWindow ??= CreateMainWindow();

    /// <summary>Discovers monitors and registers shortcuts. Called once after startup.</summary>
    public async Task StartAsync()
    {
        await Workspace.RefreshAsync(forceCapabilities: false);
        _lastActiveRefresh = DateTimeOffset.UtcNow;
        await _shortcuts.SyncAsync();
    }

    public void ShowMainWindow()
    {
        _quickSwitch?.Hide();
        MainWindow.Show();
        if (MainWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
        {
            MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
        }

        MainWindow.Activate();
    }

    public void Quit()
    {
        _mainWindow?.Close();
        _shutdown();
    }

    public void Dispose()
    {
        _tray?.Dispose();
        _shortcuts.Dispose();
        _displayChangeTimer?.Stop();
    }

    private void OpenSettings()
    {
        Main.OpenSettingsCommand.Execute(null);
        ShowMainWindow();
    }

    private void ToggleQuickSwitch()
    {
        if (_quickSwitch is { IsVisible: true })
        {
            _quickSwitch.Hide();
            return;
        }

        if (_quickSwitch is null)
        {
            _quickSwitch = new QuickSwitchWindow { DataContext = Workspace, OpenMainWindow = ShowMainWindow };
            _appearance.Attach(_quickSwitch);
        }

        _quickSwitch.ShowNearTray();
        _ = RefreshActiveInputsThrottledAsync();
    }

    private MainWindow CreateMainWindow()
    {
        var window = new MainWindow { DataContext = Main };
        _appearance.Attach(window);
        window.Activated += (_, _) => _ = RefreshActiveInputsThrottledAsync();
        window.Closed += (_, _) =>
        {
            // The window is destroyed rather than hidden, which releases its rendering resources while
            // the app waits in the tray. It is recreated on demand. Without the tray, closing ends the app.
            _mainWindow = null;
            Main.CloseEditorCommand.Execute(null);
            if (_tray is null || !_services.Settings.Current.CloseToTray)
            {
                _shutdown();
            }
        };
        window.Screens.Changed += (_, _) => ScheduleDisplayRefresh();
        return window;
    }

    private async Task RefreshActiveInputsThrottledAsync()
    {
        if (DateTimeOffset.UtcNow - _lastActiveRefresh < ActiveInputRefreshInterval)
        {
            return;
        }

        _lastActiveRefresh = DateTimeOffset.UtcNow;
        await Workspace.RefreshActiveInputsAsync();
    }

    private void ScheduleDisplayRefresh()
    {
        _displayChangeTimer ??= new DispatcherTimer(DisplayChangeDebounce, DispatcherPriority.Background, async (_, _) =>
        {
            _displayChangeTimer!.Stop();
            await Workspace.RefreshAsync(forceCapabilities: false);
        });
        _displayChangeTimer.Stop();
        _displayChangeTimer.Start();
    }

    private void ShowToastIfBackground(Feedback feedback)
    {
        if (_mainWindow is { IsActive: true } || _quickSwitch is { IsVisible: true })
        {
            return;
        }

        if (_toast is null)
        {
            _toast = new ToastWindow();
            _appearance.Attach(_toast);
        }

        _toast.Present(feedback);
    }
}
