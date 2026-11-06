using Avalonia;
using DisplaySwitch.App.Services;
using DisplaySwitch.Platform;

namespace DisplaySwitch.App;

internal static class Program
{
    /// <summary>Asks a running instance to exit; used by the installer before replacing files.</summary>
    private const string QuitArgument = "--quit";

    [STAThread]
    public static int Main(string[] args)
    {
        var quit = args.Contains(QuitArgument, StringComparer.OrdinalIgnoreCase);
        using var instance = SingleInstance.TryClaim(quit ? InstanceCommand.Quit : InstanceCommand.Activate);
        if (instance is null || quit)
        {
            // Either another instance received the request, or --quit found nothing to stop.
            return 0;
        }

        App.Instance = instance;
        App.StartInBackground = args.Contains(PlatformServices.BackgroundArgument, StringComparer.OrdinalIgnoreCase);
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Avalonia configuration; also used by the XAML previewer.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new MacOSPlatformOptions { ShowInDock = false });
}
