using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DisplaySwitch.App.ViewModels;

namespace DisplaySwitch.App.Views;

public partial class MainWindow : Window
{
    /// <summary>Width reserved for the macOS traffic-light buttons, which the OS draws at the left.</summary>
    private const double MacTrafficLightsWidth = 72;

    /// <summary>Space between the app's title-bar buttons and the caption buttons.</summary>
    private const double CaptionGap = 8;

    /// <summary>Name of the caption-button panel in Avalonia's drawn window decorations.</summary>
    private const string CaptionButtonsPanelName = "PART_OverlayPanel";

    public MainWindow()
    {
        InitializeComponent();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ActualTransparencyLevelProperty || e.Property == IsExtendedIntoWindowDecorationsProperty)
            {
                UpdateChrome();
            }
        };
        ActualThemeVariantChanged += (_, _) => UpdateChrome();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Caption buttons are laid out after the first frame.
        Dispatcher.UIThread.Post(UpdateChrome, DispatcherPriority.Loaded);
    }

    /// <summary>Number keys 1–9 switch to the matching input card, mirroring the order on screen.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (DataContext is MainViewModel { IsHome: true, IsEditorOpen: false } main
            && e.KeyModifiers == KeyModifiers.None
            && FocusManager?.GetFocusedElement() is not TextBox
            && ToDigit(e.Key) is { } digit
            && main.Workspace.SelectedMonitor is { } monitor
            && monitor.Inputs.FirstOrDefault(i => i.Position == digit) is { } card)
        {
            monitor.SwitchToCommand.Execute(card);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private static int? ToDigit(Key key) => key switch
    {
        >= Key.D1 and <= Key.D9 => key - Key.D0,
        >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad0,
        _ => null,
    };

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel main)
        {
            main.CloseEditorCommand.Execute(null);
        }
    }

    /// <summary>
    /// Uses a translucent background only when the OS actually provides Mica, and leaves room for
    /// the system caption buttons that are drawn over the extended title bar.
    /// </summary>
    private void UpdateChrome()
    {
        var mica = ActualTransparencyLevel == WindowTransparencyLevel.Mica;
        this[!BackgroundProperty] = this.GetResourceObservable(mica ? "Brush.WindowBackdrop" : "Brush.Window").ToBinding();

        var extended = IsExtendedIntoWindowDecorations;
        var captionWidth = extended ? FindCaptionButtonsWidth() : 0;
        TitleBarActions.Margin = new Thickness(0, 0, captionWidth > 0 ? captionWidth + CaptionGap : 12, 0);
        TitleBar.Margin = new Thickness(extended && OperatingSystem.IsMacOS() ? MacTrafficLightsWidth : 0, 0, 0, 0);
    }

    /// <summary>Measures the caption buttons drawn over the title bar, so app buttons never sit underneath them.</summary>
    private double FindCaptionButtonsWidth()
    {
        Visual root = this;
        while (root.GetVisualParent() is { } parent)
        {
            root = parent;
        }

        return root.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(p => p.Name == CaptionButtonsPanelName)?.Bounds.Width ?? 0;
    }
}
