using Avalonia;
using Avalonia.Controls;

namespace DisplaySwitch.App.Controls;

/// <summary>
/// Sizes its children to the largest rectangle with the given aspect ratio that fits the available
/// width, so the monitor illustration matches the real monitor's shape (32:9, 21:9, 16:9…).
/// </summary>
public sealed class AspectRatioPanel : Panel
{
    public static readonly StyledProperty<double> RatioProperty =
        AvaloniaProperty.Register<AspectRatioPanel, double>(nameof(Ratio), 16.0 / 9.0);

    static AspectRatioPanel() => AffectsMeasure<AspectRatioPanel>(RatioProperty);

    public double Ratio
    {
        get => GetValue(RatioProperty);
        set => SetValue(RatioProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Fit(availableSize);
        foreach (var child in Children)
        {
            child.Measure(size);
        }

        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = Fit(finalSize);
        var origin = new Point((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2);
        foreach (var child in Children)
        {
            child.Arrange(new Rect(origin, size));
        }

        return finalSize;
    }

    private Size Fit(Size available)
    {
        var ratio = Ratio > 0 ? Ratio : 16.0 / 9.0;
        var width = double.IsInfinity(available.Width) ? 480 : available.Width;
        var height = width / ratio;
        if (!double.IsInfinity(available.Height) && height > available.Height)
        {
            height = available.Height;
            width = height * ratio;
        }

        return new Size(width, height);
    }
}
