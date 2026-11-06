namespace DisplaySwitch.Core.Platform;

/// <summary>Controls whether the application starts when the user signs in.</summary>
/// <remarks>
/// The operating system is the source of truth, not the settings file, so a change made in the OS
/// startup-apps panel is always reflected.
/// </remarks>
public interface IAutostartService
{
    bool IsSupported { get; }

    bool IsEnabled { get; }

    /// <exception cref="IOException">The startup entry could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The startup entry could not be written.</exception>
    void SetEnabled(bool enabled);
}
