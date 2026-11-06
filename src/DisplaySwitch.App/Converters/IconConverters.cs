using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DisplaySwitch.App.ViewModels;
using DisplaySwitch.Core.Inputs;

namespace DisplaySwitch.App.Converters;

/// <summary>Maps domain values to the line icons defined in <c>Design/Icons.axaml</c>.</summary>
public static class IconConverters
{
    public static FuncValueConverter<InputIcon, Geometry?> ForInput { get; } = new(icon => Find($"Icon.{icon}"));

    public static FuncValueConverter<FeedbackKind, Geometry?> ForFeedback { get; } = new(kind => Find(kind switch
    {
        FeedbackKind.Success => "Icon.Success",
        FeedbackKind.Warning => "Icon.Warning",
        FeedbackKind.Error => "Icon.Error",
        _ => "Icon.Info",
    }));

    private static Geometry? Find(string key) =>
        Application.Current?.TryGetResource(key, null, out var resource) == true ? resource as Geometry : null;
}
