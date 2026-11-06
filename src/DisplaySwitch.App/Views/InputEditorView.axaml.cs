using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using DisplaySwitch.App.ViewModels;

namespace DisplaySwitch.App.Views;

public partial class InputEditorView : UserControl
{
    public InputEditorView()
    {
        InitializeComponent();
        Recorder.Recorded = gesture => (DataContext as InputEditorViewModel)?.RecordShortcut(gesture);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Move focus into the sheet so keyboard and screen-reader users land in the dialog.
        var first = DataContext is InputEditorViewModel { IsNew: true } ? (Control)CodeBox : NameBox;
        first.Focus(NavigationMethod.Tab);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key == Key.Escape && DataContext is InputEditorViewModel editor)
        {
            editor.CancelCommand.Execute(null);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
