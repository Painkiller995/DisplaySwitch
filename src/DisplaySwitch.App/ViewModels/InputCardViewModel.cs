using CommunityToolkit.Mvvm.ComponentModel;
using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Shortcuts;

namespace DisplaySwitch.App.ViewModels;

/// <summary>One selectable input card.</summary>
public sealed partial class InputCardViewModel(InputSource source, int position, HotkeyGesture? shortcut) : ObservableObject
{
    public InputSource Source { get; } = source;

    /// <summary>1-based position, also the in-window key that selects this input.</summary>
    public int Position { get; } = position;

    public HotkeyGesture? Shortcut { get; } = shortcut;

    public string DisplayName => Source.DisplayName;

    /// <summary>The connector name, shown under the device name only when the two differ.</summary>
    public string? PortCaption => string.IsNullOrWhiteSpace(Source.Label) ? null : Source.PortName;

    public InputIcon Icon => Source.Icon;

    public IReadOnlyList<string> ShortcutParts => Shortcut?.Parts ?? [];

    public bool HasShortcut => Shortcut is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleName))]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial bool IsSwitching { get; set; }

    /// <summary>A complete spoken description, because screen readers do not announce visual chips.</summary>
    public string AccessibleName
    {
        get
        {
            var parts = new List<string> { DisplayName };
            if (PortCaption is not null)
            {
                parts.Add(PortCaption);
            }

            if (IsActive)
            {
                parts.Add("currently on screen");
            }

            if (Shortcut is { } gesture)
            {
                parts.Add($"shortcut {string.Join(' ', gesture.Parts)}");
            }

            return string.Join(", ", parts);
        }
    }
}
