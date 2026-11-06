namespace DisplaySwitch.Core.Platform;

/// <summary>Per-user storage locations, following each operating system's conventions.</summary>
public sealed record AppPaths(string DataDirectory)
{
    public const string AppName = "DisplaySwitch";

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>
    /// %APPDATA%\DisplaySwitch on Windows, ~/Library/Application Support/DisplaySwitch on macOS, and
    /// $XDG_CONFIG_HOME/displayswitch (usually ~/.config/displayswitch) on Linux.
    /// </summary>
    public static AppPaths ForCurrentUser()
    {
        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new AppPaths(Path.Combine(home, "Library", "Application Support", AppName));
        }

        if (OperatingSystem.IsLinux())
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var root = string.IsNullOrWhiteSpace(configHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
                : configHome;
            return new AppPaths(Path.Combine(root, AppName.ToLowerInvariant()));
        }

        return new AppPaths(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName));
    }
}
