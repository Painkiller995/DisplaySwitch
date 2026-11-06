using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.App;

public partial class App : Application
{
    /// <summary>Set by <see cref="Program"/> before startup.</summary>
    internal static bool StartInBackground { get; set; }

    /// <summary>Set by <see cref="Program"/>; lets a second launch bring this instance forward.</summary>
    internal static Services.SingleInstance? Instance { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // A tray app keeps running with no windows open; it exits through Quit.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var services = AppServices.CreateForCurrentPlatform();
            LogUnhandledExceptions(services.LoggerFactory.CreateLogger<App>());
            var shell = new AppShell(this, services, enableTray: true, shutdown: () => desktop.Shutdown());

            // The tray has nothing to offer until onboarding is complete.
            if (!StartInBackground || !services.Settings.Current.OnboardingCompleted)
            {
                shell.ShowMainWindow();
            }

            Instance?.Listen(command => Dispatcher.UIThread.Post(command == Services.InstanceCommand.Quit ? shell.Quit : shell.ShowMainWindow));
            _ = shell.StartAsync();
            desktop.Exit += (_, _) =>
            {
                // Settings saves are small and already queued; wait so the last change is never lost.
                services.Settings.FlushAsync().Wait(TimeSpan.FromSeconds(2));
                shell.Dispose();
                services.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Records crashes in the log so they can be diagnosed. Exceptions are not swallowed: an
    /// unexpected state is safer to restart from than to continue in.
    /// </summary>
    private static void LogUnhandledExceptions(ILogger logger)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(logger, e.ExceptionObject as Exception);
        Dispatcher.UIThread.UnhandledException += (_, e) => LogCrash(logger, e.Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => LogUnobserved(logger, e.Exception);
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogCrash(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unobserved task exception")]
    private static partial void LogUnobserved(ILogger logger, Exception exception);
}
