using DisplaySwitch.Core.Monitors;
using DisplaySwitch.Core.Platform;
using DisplaySwitch.Core.Shortcuts;
using DisplaySwitch.Platform.Linux;
using DisplaySwitch.Platform.MacOS;
using DisplaySwitch.Platform.Windows;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.Platform;

/// <summary>The operating-system integrations for the current platform, created in one place.</summary>
public sealed class PlatformServices : IDisposable
{
    /// <summary>Command-line flag passed by autostart entries so the app starts in the tray.</summary>
    public const string BackgroundArgument = "--background";

    private readonly Func<bool> _prefersReducedMotion;

    /// <summary>Assembles platform services explicitly; tests use this to substitute simulated hardware.</summary>
    public PlatformServices(IDisplayBackend displays, IGlobalHotkeyService hotkeys, IAutostartService autostart, Func<bool> prefersReducedMotion)
    {
        Displays = displays;
        Hotkeys = hotkeys;
        Autostart = autostart;
        _prefersReducedMotion = prefersReducedMotion;
    }

    public IDisplayBackend Displays { get; }

    public IGlobalHotkeyService Hotkeys { get; }

    public IAutostartService Autostart { get; }

    /// <summary>Whether the operating system asks applications to minimize animation.</summary>
    public bool PrefersReducedMotion => _prefersReducedMotion();

    public static PlatformServices CreateForCurrentOS(ILoggerFactory loggerFactory, string executablePath)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        if (OperatingSystem.IsWindows())
        {
            return new PlatformServices(
                new WindowsDisplayBackend(loggerFactory.CreateLogger<WindowsDisplayBackend>()),
                new WindowsHotkeyService(loggerFactory.CreateLogger<WindowsHotkeyService>()),
                new WindowsAutostartService(executablePath, BackgroundArgument),
                WindowsDisplayPreferences.PrefersReducedMotion);
        }

        if (OperatingSystem.IsMacOS())
        {
            return new PlatformServices(
                new MacDisplayBackend(loggerFactory.CreateLogger<MacDisplayBackend>()),
                new UnsupportedHotkeyService(),
                FileAutostartService.ForMacOS(executablePath, BackgroundArgument),
                static () => false);
        }

        if (OperatingSystem.IsLinux())
        {
            // An AppImage runs from a temporary mount; autostart must point at the AppImage file itself.
            var launcher = Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } appImage ? appImage : executablePath;
            return new PlatformServices(
                new LinuxDisplayBackend(loggerFactory.CreateLogger<LinuxDisplayBackend>()),
                new UnsupportedHotkeyService(),
                FileAutostartService.ForLinux(launcher, BackgroundArgument),
                static () => false);
        }

        throw new PlatformNotSupportedException("DisplaySwitch supports Windows, macOS and Linux.");
    }

    public void Dispose() => Hotkeys.Dispose();

    /// <summary>
    /// Used where no reliable global-shortcut API exists yet (macOS needs Carbon event hooks, and
    /// Wayland deliberately prevents global key grabs). The UI explains this instead of failing silently.
    /// </summary>
    private sealed class UnsupportedHotkeyService : IGlobalHotkeyService
    {
        public bool IsSupported => false;

        public event EventHandler<HotkeyGesture>? Pressed
        {
            add { }
            remove { }
        }

        public Task<IReadOnlySet<HotkeyGesture>> RegisterAsync(IReadOnlyCollection<HotkeyGesture> gestures) =>
            Task.FromResult<IReadOnlySet<HotkeyGesture>>(gestures.ToHashSet());

        public void Dispose()
        {
        }
    }
}
