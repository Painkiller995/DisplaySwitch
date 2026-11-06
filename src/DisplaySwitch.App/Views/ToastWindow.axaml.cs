using Avalonia;
using Avalonia.Controls;
using DisplaySwitch.App.ViewModels;

namespace DisplaySwitch.App.Views;

public partial class ToastWindow : Window
{
    private static readonly TimeSpan VisibleDuration = TimeSpan.FromSeconds(2.5);

    /// <summary>Distance from the bottom of the working area, clear of the taskbar and typical OS toasts.</summary>
    private const double BottomOffset = 96;

    private Feedback? _current;

    public ToastWindow() => InitializeComponent();

    /// <summary>Shows <paramref name="feedback"/> centered near the bottom of the primary screen.</summary>
    public async void Present(Feedback feedback)
    {
        _current = feedback;
        DataContext = feedback;
        Show();
        if (Screens.Primary is { } screen)
        {
            var scale = screen.Scaling;
            var work = screen.WorkingArea;
            Position = new PixelPoint(
                work.X + ((work.Width - (int)(Bounds.Width * scale)) / 2),
                work.Bottom - (int)((Bounds.Height + BottomOffset) * scale));
        }

        await Task.Delay(VisibleDuration);
        // A newer toast that replaced this one owns hiding.
        if (ReferenceEquals(_current, feedback))
        {
            Hide();
        }
    }
}
