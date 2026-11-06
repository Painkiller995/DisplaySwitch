using System.Text.Json.Serialization;

namespace DisplaySwitch.Core.Shortcuts;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,

    /// <summary>The Windows key, or Command on macOS.</summary>
    Meta = 8,
}

/// <summary>Keys that can complete a global shortcut. Kept small so every platform can map them reliably.</summary>
public enum HotkeyKey
{
    None,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
}

/// <summary>A system-wide keyboard shortcut such as Ctrl+Alt+1.</summary>
[JsonConverter(typeof(HotkeyGestureJsonConverter))]
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, HotkeyKey Key)
{
    private const char Separator = '+';

    private static readonly (HotkeyModifiers Flag, string Name)[] ModifierNames =
    [
        (HotkeyModifiers.Control, "Ctrl"),
        (HotkeyModifiers.Alt, "Alt"),
        (HotkeyModifiers.Shift, "Shift"),
        (HotkeyModifiers.Meta, "Win"),
    ];

    /// <summary>
    /// Whether the gesture is acceptable as a global shortcut. It needs Ctrl, Alt or Meta, because
    /// Shift+letter (or a bare key) would capture ordinary typing in every application.
    /// </summary>
    public bool IsValid =>
        Key != HotkeyKey.None
        && Enum.IsDefined(Key)
        && (Modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Meta)) != 0
        && (Modifiers & ~(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Meta)) == 0;

    /// <summary>The individual key names in canonical order, for rendering as keycaps.</summary>
    public IReadOnlyList<string> Parts
    {
        get
        {
            var modifiers = Modifiers;
            var parts = ModifierNames.Where(m => modifiers.HasFlag(m.Flag)).Select(m => m.Name).ToList();
            parts.Add(KeyName(Key));
            return parts;
        }
    }

    public override string ToString() => string.Join(Separator, Parts);

    /// <summary>Parses text such as "Ctrl+Alt+1". Modifier names are case-insensitive and order-independent.</summary>
    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var tokens = text.Split(Separator, StringSplitOptions.TrimEntries);
        var modifiers = HotkeyModifiers.None;
        foreach (var token in tokens[..^1])
        {
            var flag = token.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => HotkeyModifiers.Control,
                "ALT" or "OPTION" => HotkeyModifiers.Alt,
                "SHIFT" => HotkeyModifiers.Shift,
                "WIN" or "META" or "CMD" or "COMMAND" or "SUPER" => HotkeyModifiers.Meta,
                _ => HotkeyModifiers.None,
            };

            if (flag == HotkeyModifiers.None || modifiers.HasFlag(flag))
            {
                return false;
            }

            modifiers |= flag;
        }

        if (!TryParseKey(tokens[^1], out var key))
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, key);
        return gesture.IsValid;
    }

    private static string KeyName(HotkeyKey key) =>
        key is >= HotkeyKey.D0 and <= HotkeyKey.D9 ? ((int)(key - HotkeyKey.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture) : key.ToString();

    private static bool TryParseKey(string token, out HotkeyKey key)
    {
        key = HotkeyKey.None;
        if (token.Length == 1 && char.IsAsciiDigit(token[0]))
        {
            key = HotkeyKey.D0 + (token[0] - '0');
            return true;
        }

        // Single letters and F-keys share their enum names; reject numeric strings that Enum.TryParse would accept.
        return !token.All(char.IsAsciiDigit)
            && Enum.TryParse(token, ignoreCase: true, out key)
            && key is not HotkeyKey.None and not (>= HotkeyKey.D0 and <= HotkeyKey.D9);
    }
}
