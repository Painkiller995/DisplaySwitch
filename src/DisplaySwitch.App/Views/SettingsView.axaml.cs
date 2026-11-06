using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DisplaySwitch.App.ViewModels;

namespace DisplaySwitch.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    /// <summary>Opening a folder is a platform UI concern, so it lives in the view rather than the view model.</summary>
    private async void OnOpenLogsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel settings || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        Directory.CreateDirectory(settings.LogDirectory);
        await topLevel.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(settings.LogDirectory));
    }
}
