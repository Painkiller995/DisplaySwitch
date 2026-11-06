using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using DisplaySwitch.App.Views;
using DisplaySwitch.Core.Monitors;
using DisplaySwitch.Core.Platform;
using DisplaySwitch.Core.Settings;
using DisplaySwitch.Core.Shortcuts;
using DisplaySwitch.Core.Switching;
using DisplaySwitch.Platform;
using Microsoft.Extensions.Logging.Abstractions;

[assembly: AvaloniaTestApplication(typeof(DisplaySwitch.App.Tests.TestAppBuilder))]

namespace DisplaySwitch.App.Tests;

public static class TestAppBuilder
{
    /// <summary>Real Skia rendering (not the stub renderer), so tests can capture frames for visual review.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
}

/// <summary>
/// Builds the real application shell around simulated hardware. Everything above the platform
/// layer (view models, views, settings, switching logic) is the production code.
/// </summary>
internal sealed class TestHost : IDisposable
{
    private static readonly DdcTiming FastTiming = new()
    {
        CommandTimeout = TimeSpan.FromMilliseconds(500),
        CapabilitiesTimeout = TimeSpan.FromMilliseconds(500),
        MaxAttempts = 2,
        RetryDelay = TimeSpan.FromMilliseconds(1),
        SwitchSettleDelay = TimeSpan.FromMilliseconds(5),
        VerifyPollInterval = TimeSpan.FromMilliseconds(5),
        VerifyWindow = TimeSpan.FromMilliseconds(80),
    };

    private readonly string _directory = Directory.CreateTempSubdirectory("displayswitch-ui-").FullName;
    private readonly AppServices _services;

    private TestHost(IDisplayBackend backend, Action<string>? prepareSettingsFile, Func<AppSettings, AppSettings>? configure)
    {
        var paths = new AppPaths(_directory);
        prepareSettingsFile?.Invoke(paths.SettingsFile);
        var store = JsonSettingsStore.Load(paths.SettingsFile, NullLogger.Instance);
        if (configure is not null)
        {
            store.Update(configure);
        }

        Settings = store;
        Hotkeys = new FakeHotkeyService();
        var platform = new PlatformServices(backend, Hotkeys, new FakeAutostartService(), () => false);
        _services = new AppServices(paths, NullLoggerFactory.Instance, store, platform, FastTiming);
        Shell = new AppShell(Application.Current!, _services, enableTray: false, shutdown: () => { });
    }

    public AppShell Shell { get; }

    public ISettingsStore Settings { get; }

    public FakeHotkeyService Hotkeys { get; }

    public MainWindow Window => Shell.MainWindow;

    /// <summary>Settings for a user who finished onboarding, so tests start on the home page.</summary>
    public static AppSettings Onboarded(AppSettings settings) => settings with { OnboardingCompleted = true };

    public static async Task<TestHost> StartAsync(
        IDisplayBackend backend,
        Func<AppSettings, AppSettings>? configure = null,
        Action<string>? prepareSettingsFile = null,
        double width = 900,
        double height = 680)
    {
        var host = new TestHost(backend, prepareSettingsFile, configure);
        host.Window.Width = width;
        host.Window.Height = height;
        host.Shell.ShowMainWindow();
        await host.Shell.StartAsync();
        await host.Shell.Workspace.PictureLoad;
        Pump();
        return host;
    }

    /// <summary>Runs queued UI work and renders a frame.</summary>
    public static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Waits for asynchronous work (DDC simulation runs on the thread pool) while keeping the UI responsive.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline)
            {
                throw new TimeoutException("The UI did not reach the expected state in time.");
            }

            Pump();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Pump();
    }

    /// <summary>Saves the current frame under TestResults/screenshots for visual review.</summary>
    public void Capture(string name)
    {
        Pump();
        var directory = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(directory);
        Window.CaptureRenderedFrame()?.Save(Path.Combine(directory, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    public void Dispose()
    {
        Window.Close();
        Shell.Dispose();
        _services.Dispose();
        Directory.Delete(_directory, recursive: true);
    }
}

/// <summary>Records registrations and lets tests press shortcuts.</summary>
internal sealed class FakeHotkeyService : IGlobalHotkeyService
{
    public bool IsSupported => true;

    public HashSet<HotkeyGesture> OwnedByOtherApps { get; } = [];

    public IReadOnlyCollection<HotkeyGesture> Registered { get; private set; } = [];

    public event EventHandler<HotkeyGesture>? Pressed;

    public Task<IReadOnlySet<HotkeyGesture>> RegisterAsync(IReadOnlyCollection<HotkeyGesture> gestures)
    {
        Registered = gestures;
        return Task.FromResult<IReadOnlySet<HotkeyGesture>>(gestures.Where(OwnedByOtherApps.Contains).ToHashSet());
    }

    public void Press(HotkeyGesture gesture) => Pressed?.Invoke(this, gesture);

    public void Dispose()
    {
    }
}

internal sealed class FakeAutostartService : IAutostartService
{
    public bool IsSupported => true;

    public bool IsEnabled { get; private set; }

    public void SetEnabled(bool enabled) => IsEnabled = enabled;
}
