using Avalonia.Controls;

namespace DisplaySwitch.App.Views;

public partial class Keycaps : ItemsControl
{
    public Keycaps() => InitializeComponent();

    protected override Type StyleKeyOverride => typeof(ItemsControl);
}
