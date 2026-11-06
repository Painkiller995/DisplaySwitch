using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using DisplaySwitch.Core.Shortcuts;

namespace DisplaySwitch.App.Controls;

/// <summary>
/// A focusable field that captures a key combination instead of text. Tab and Shift+Tab still move
/// focus, so keyboard users are never trapped, and Escape leaves the field unchanged.
/// </summary>
public sealed class ShortcutRecorder : Button
{
    public static readonly StyledProperty<Action<HotkeyGesture>?> RecordedProperty =
        AvaloniaProperty.Register<ShortcutRecorder, Action<HotkeyGesture>?>(nameof(Recorded));

    protected override Type StyleKeyOverride => typeof(Button);

    /// <summary>Receives each gesture the user presses, valid or not, so the caller can explain problems.</summary>
    public Action<HotkeyGesture>? Recorded
    {
        get => GetValue(RecordedProperty);
        set => SetValue(RecordedProperty, value);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key is Key.Tab or Key.Escape || IsModifierKey(e.Key))
        {
            base.OnKeyDown(e);
            return;
        }

        e.Handled = true;
        Recorded?.Invoke(new HotkeyGesture(ToModifiers(e.KeyModifiers), ToHotkeyKey(e.Key)));
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private static HotkeyModifiers ToModifiers(KeyModifiers modifiers) =>
        (modifiers.HasFlag(KeyModifiers.Control) ? HotkeyModifiers.Control : HotkeyModifiers.None)
        | (modifiers.HasFlag(KeyModifiers.Alt) ? HotkeyModifiers.Alt : HotkeyModifiers.None)
        | (modifiers.HasFlag(KeyModifiers.Shift) ? HotkeyModifiers.Shift : HotkeyModifiers.None)
        | (modifiers.HasFlag(KeyModifiers.Meta) ? HotkeyModifiers.Meta : HotkeyModifiers.None);

    private static HotkeyKey ToHotkeyKey(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => HotkeyKey.D0 + (key - Key.D0),
        >= Key.NumPad0 and <= Key.NumPad9 => HotkeyKey.D0 + (key - Key.NumPad0),
        >= Key.A and <= Key.Z => HotkeyKey.A + (key - Key.A),
        >= Key.F1 and <= Key.F12 => HotkeyKey.F1 + (key - Key.F1),
        _ => HotkeyKey.None,
    };
}
