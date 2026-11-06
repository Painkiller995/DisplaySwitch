namespace DisplaySwitch.Core.Shortcuts;

/// <summary>Registers system-wide keyboard shortcuts with the operating system.</summary>
public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>Whether this platform supports global shortcuts at all.</summary>
    bool IsSupported { get; }

    /// <summary>Raised on a background thread when a registered gesture is pressed.</summary>
    event EventHandler<HotkeyGesture>? Pressed;

    /// <summary>
    /// Replaces all registrations with <paramref name="gestures"/>.
    /// </summary>
    /// <returns>
    /// The gestures that could not be registered, usually because another application already owns them.
    /// </returns>
    Task<IReadOnlySet<HotkeyGesture>> RegisterAsync(IReadOnlyCollection<HotkeyGesture> gestures);
}
