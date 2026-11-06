using CommunityToolkit.Mvvm.ComponentModel;
using DisplaySwitch.Core.Inputs;

namespace DisplaySwitch.App.ViewModels;

/// <summary>One option in the icon picker.</summary>
public sealed partial class IconChoiceViewModel(InputIcon icon, Action<InputIcon> onSelected) : ObservableObject
{
    public InputIcon Icon { get; } = icon;

    public string Name => Names[Icon];

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>User-facing icon names, also used as accessible names for the picker.</summary>
    public static IReadOnlyDictionary<InputIcon, string> Names { get; } = new Dictionary<InputIcon, string>
    {
        [InputIcon.Port] = "Generic input",
        [InputIcon.Desktop] = "Desktop computer",
        [InputIcon.Laptop] = "Laptop",
        [InputIcon.GameConsole] = "Game console",
        [InputIcon.MediaPlayer] = "Media player",
        [InputIcon.Tablet] = "Tablet",
        [InputIcon.Camera] = "Camera",
        [InputIcon.Dock] = "Dock",
    };

    public static IReadOnlyList<IconChoiceViewModel> CreateAll(InputIcon selected, Action<InputIcon> onSelected) =>
        [.. Enum.GetValues<InputIcon>().Select(icon => new IconChoiceViewModel(icon, onSelected) { IsSelected = icon == selected })];

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            onSelected(Icon);
        }
    }
}
