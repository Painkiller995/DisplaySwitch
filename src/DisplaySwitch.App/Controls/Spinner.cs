using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace DisplaySwitch.App.Controls;

/// <summary>
/// An indeterminate progress arc. Rotation is defined in Styles.axaml under ".animated", so the arc
/// stays still (but visible) when the user prefers reduced motion.
/// </summary>
public sealed class Spinner : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Spinner>();

    private const double ArcSweepDegrees = 270;
    private const double StrokeRatio = 0.12;

    static Spinner()
    {
        AffectsRender<Spinner>(ForegroundProperty);
        FocusableProperty.OverrideDefaultValue<Spinner>(false);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }

        var size = Math.Min(Bounds.Width, Bounds.Height);
        var thickness = Math.Max(1.5, size * StrokeRatio);
        var radius = (size - thickness) / 2;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var end = ArcSweepDegrees * Math.PI / 180;

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(center.X + radius, center.Y), isFilled: false);
            g.ArcTo(new Point(center.X + (radius * Math.Cos(end)), center.Y + (radius * Math.Sin(end))), new Size(radius, radius), 0, isLargeArc: true, SweepDirection.Clockwise);
            g.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, new Pen(brush, thickness, lineCap: PenLineCap.Round), geometry);
    }
}
