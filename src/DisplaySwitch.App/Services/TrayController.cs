using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using DisplaySwitch.App.ViewModels;

namespace DisplaySwitch.App.Services;

/// <summary>
/// The system tray (Windows, Linux) or menu bar (macOS) icon. Left-click opens the quick switcher.
/// The menu lists every input with a check mark on the live one, so switching takes two clicks.
/// </summary>
internal sealed class TrayController : IDisposable
{
    private readonly Application _application;
    private readonly WorkspaceViewModel _workspace;
    private readonly TrayIcon _tray;
    private readonly Action _openMainWindow;
    private readonly Action _openSettings;
    private readonly Action _quit;

    public TrayController(Application application, WorkspaceViewModel workspace, Action toggleQuickSwitch, Action openMainWindow, Action openSettings, Action quit)
    {
        _application = application;
        _workspace = workspace;
        _openMainWindow = openMainWindow;
        _openSettings = openSettings;
        _quit = quit;

        _tray = new TrayIcon
        {
            Icon = LoadIcon(),
            ToolTipText = "DisplaySwitch",
            Menu = [],
            IsVisible = true,
        };
        if (OperatingSystem.IsMacOS())
        {
            // Template icons adapt to light and dark menu bars automatically.
            MacOSProperties.SetIsTemplateIcon(_tray, true);
        }

        _tray.Clicked += (_, _) => toggleQuickSwitch();
        TrayIcon.SetIcons(_application, [_tray]);
        _workspace.MonitorsChanged += OnMonitorsChanged;
        Rebuild();
    }

    public void Dispose()
    {
        _workspace.MonitorsChanged -= OnMonitorsChanged;
        _tray.IsVisible = false;
        TrayIcon.SetIcons(_application, []);
        _tray.Dispose();
    }

    private void OnMonitorsChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        var menu = _tray.Menu!;
        menu.Items.Clear();

        var controllable = _workspace.Monitors.Where(m => m.IsControllable && m.HasInputs).ToList();
        foreach (var monitor in controllable)
        {
            if (controllable.Count > 1)
            {
                menu.Items.Add(new NativeMenuItem(monitor.DisplayName) { IsEnabled = false });
            }

            foreach (var card in monitor.Inputs)
            {
                var item = new NativeMenuItem(card.DisplayName)
                {
                    ToggleType = MenuItemToggleType.Radio,
                    IsChecked = card.IsActive,
                };
                item.Click += (_, _) => monitor.SwitchToCommand.Execute(card);
                menu.Items.Add(item);
            }

            menu.Items.Add(new NativeMenuItemSeparator());
        }

        if (controllable.Count == 0)
        {
            menu.Items.Add(new NativeMenuItem(_workspace.IsScanning ? "Looking for monitors…" : "No controllable monitors") { IsEnabled = false });
            menu.Items.Add(new NativeMenuItemSeparator());
        }

        menu.Items.Add(CreateItem("Open DisplaySwitch", _openMainWindow));
        menu.Items.Add(CreateItem("Settings…", _openSettings));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(CreateItem("Quit DisplaySwitch", _quit));

        var active = _workspace.SelectedMonitor?.ActiveInput?.DisplayName;
        _tray.ToolTipText = active is null ? "DisplaySwitch" : $"DisplaySwitch · {active}";
    }

    private static NativeMenuItem CreateItem(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }

    private static WindowIcon LoadIcon()
    {
        var asset = OperatingSystem.IsMacOS() ? "tray-template.png" : OperatingSystem.IsWindows() ? "displayswitch.ico" : "displayswitch-64.png";
        using var stream = AssetLoader.Open(new Uri($"avares://DisplaySwitch/Assets/{asset}"));
        return new WindowIcon(stream);
    }
}
