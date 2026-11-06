using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DisplaySwitch.App.Views;

public partial class QuickSwitchWindow : Window
{
    /// <summary>Gap between the flyout and the taskbar or screen edge, in device-independent pixels.</summary>
    private const double EdgeGap = 4;

    public QuickSwitchWindow()
    {
        InitializeComponent();
        Deactivated += (_, _) => Hide();
    }

    /// <summary>Invoked when the user asks for the full window.</summary>
    public Action? OpenMainWindow { get; set; }

    /// <summary>
    /// Shows the flyout in the corner nearest the taskbar or menu bar, which is where the tray icon is.
    /// The taskbar edge is wherever the screen's working area is inset from its bounds.
    /// </summary>
    public void ShowNearTray()
    {
        Show();
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
        {
            Activate();
            return;
        }

        var scale = screen.Scaling;
        var work = screen.WorkingArea;
        var bounds = screen.Bounds;
        var width = (int)(Bounds.Width * scale);
        var height = (int)(Bounds.Height * scale);
        var gap = (int)(EdgeGap * scale);

        var taskbarAtTop = work.Y > bounds.Y;
        var taskbarAtLeft = work.X > bounds.X;
        var x = taskbarAtLeft ? work.X + gap : work.Right - width - gap;
        var y = taskbarAtTop ? work.Y + gap : work.Bottom - height - gap;
        Position = new PixelPoint(x, y);
        Activate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnOpenAppClicked(object? sender, RoutedEventArgs e)
    {
        Hide();
        OpenMainWindow?.Invoke();
    }
}
