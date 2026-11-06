using DisplaySwitch.App.Services;
using DisplaySwitch.Core.Platform;
using DisplaySwitch.Core.Settings;
using DisplaySwitch.Core.Switching;
using DisplaySwitch.Platform;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.App;

/// <summary>
/// The composition root's non-UI services. Production code builds them for the real platform;
/// UI tests build them around simulated monitors.
/// </summary>
internal sealed class AppServices : IDisposable
{
    public AppServices(AppPaths paths, ILoggerFactory loggerFactory, ISettingsStore settings, PlatformServices platform, DdcTiming timing)
    {
        Paths = paths;
        LoggerFactory = loggerFactory;
        Settings = settings;
        Platform = platform;
        Directory = new MonitorDirectory(platform.Displays, new SettingsCapabilitiesCache(settings), timing, loggerFactory);
    }

    public AppPaths Paths { get; }

    public ILoggerFactory LoggerFactory { get; }

    public ISettingsStore Settings { get; }

    public PlatformServices Platform { get; }

    public MonitorDirectory Directory { get; }

    public static AppServices CreateForCurrentPlatform()
    {
        var paths = AppPaths.ForCurrentUser();
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.AddProvider(new FileLoggerProvider(paths.LogDirectory, TimeProvider.System)).SetMinimumLevel(LogLevel.Information));
        var settings = JsonSettingsStore.Load(paths.SettingsFile, loggerFactory.CreateLogger<JsonSettingsStore>());
        var platform = PlatformServices.CreateForCurrentOS(loggerFactory, Environment.ProcessPath ?? AppPaths.AppName);
        return new AppServices(paths, loggerFactory, settings, platform, DdcTiming.Default);
    }

    public void Dispose()
    {
        Directory.Dispose();
        Platform.Dispose();
        LoggerFactory.Dispose();
    }
}
