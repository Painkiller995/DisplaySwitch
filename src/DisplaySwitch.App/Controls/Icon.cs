using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace DisplaySwitch.App.Controls;

/// <summary>
/// Draws a 24×24 line icon from <c>Design/Icons.axaml</c>, scaled to the control's size.
/// </summary>
/// <remarks>
/// A plain Path with Stretch would scale each glyph to its own bounding box, so icons would differ
/// in size and stroke weight. Mapping the shared 24-unit grid keeps every glyph consistent.
/// Inherits <see cref="TextElement.ForegroundProperty"/> so icons follow the text color of their
/// button or card, including hover, disabled and theme changes.
/// </remarks>
public sealed class Icon : Control
{
    private const double GridSize = 24;

    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<Icon, Geometry?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Icon>();

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(StrokeThickness), 1.6);

    static Icon()
    {
        AffectsRender<Icon>(DataProperty, ForegroundProperty, StrokeThicknessProperty);
        WidthProperty.OverrideDefaultValue<Icon>(20);
        HeightProperty.OverrideDefaultValue<Icon>(20);
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Stroke width in grid units (1/24 of the icon size).</summary>
    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Data is not { } geometry || Foreground is not { } brush)
        {
            return;
        }

        var scale = Math.Min(Bounds.Width, Bounds.Height) / GridSize;
        var offset = new Point((Bounds.Width - (GridSize * scale)) / 2, (Bounds.Height - (GridSize * scale)) / 2);
        var pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset.X, offset.Y)))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }
}
