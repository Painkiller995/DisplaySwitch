using DisplaySwitch.Core.Inputs;

namespace DisplaySwitch.Core.Shortcuts;

/// <summary>Binds a global shortcut to selecting one input on one monitor.</summary>
public sealed record ShortcutBinding(HotkeyGesture Gesture, string MonitorKey, InputCode Input);

/// <summary>Rules for assigning shortcuts without collisions.</summary>
public static class ShortcutRules
{
    /// <summary>Default gestures offered for a monitor's first inputs: Ctrl+Alt+1, Ctrl+Alt+2, …</summary>
    public static IEnumerable<HotkeyGesture> DefaultGestures() =>
        Enumerable.Range(1, 9).Select(n => new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKey.D0 + n));

    /// <summary>
    /// Finds an existing binding that already uses <paramref name="gesture"/> for a different input,
    /// or returns <see langword="null"/> if the gesture is free.
    /// </summary>
    public static ShortcutBinding? FindConflict(IEnumerable<ShortcutBinding> bindings, HotkeyGesture gesture, string monitorKey, InputCode input) =>
        bindings.FirstOrDefault(b => b.Gesture == gesture && !IsFor(b, monitorKey, input));

    /// <summary>Returns <paramref name="bindings"/> with <paramref name="gesture"/> assigned to the input, replacing its previous shortcut.</summary>
    public static IReadOnlyList<ShortcutBinding> Assign(IEnumerable<ShortcutBinding> bindings, HotkeyGesture gesture, string monitorKey, InputCode input) =>
        [.. bindings.Where(b => b.Gesture != gesture && !IsFor(b, monitorKey, input)), new ShortcutBinding(gesture, monitorKey, input)];

    public static IReadOnlyList<ShortcutBinding> Remove(IEnumerable<ShortcutBinding> bindings, string monitorKey, InputCode input) =>
        [.. bindings.Where(b => !IsFor(b, monitorKey, input))];

    public static ShortcutBinding? Find(IEnumerable<ShortcutBinding> bindings, string monitorKey, InputCode input) =>
        bindings.FirstOrDefault(b => IsFor(b, monitorKey, input));

    private static bool IsFor(ShortcutBinding binding, string monitorKey, InputCode input) =>
        binding.Input == input && string.Equals(binding.MonitorKey, monitorKey, StringComparison.OrdinalIgnoreCase);
}
