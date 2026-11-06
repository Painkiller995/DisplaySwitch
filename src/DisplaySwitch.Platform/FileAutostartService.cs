using System.Security;
using DisplaySwitch.Core.Platform;

namespace DisplaySwitch.Platform;

/// <summary>
/// Autostart through a per-user file: an XDG autostart entry on Linux or a LaunchAgent on macOS.
/// </summary>
internal sealed class FileAutostartService(string entryPath, string content) : IAutostartService
{
    public bool IsSupported => true;

    public bool IsEnabled => File.Exists(entryPath) && File.ReadAllText(entryPath) == content;

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(entryPath)!);
            File.WriteAllText(entryPath, content);
        }
        else if (File.Exists(entryPath))
        {
            File.Delete(entryPath);
        }
    }

    public static FileAutostartService ForLinux(string executablePath, string arguments)
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var root = string.IsNullOrWhiteSpace(configHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : configHome;
        var content = $"""
            [Desktop Entry]
            Type=Application
            Name={AppPaths.AppName}
            Exec="{executablePath}" {arguments}
            Icon=displayswitch
            X-GNOME-Autostart-enabled=true
            NoDisplay=true

            """;
        return new FileAutostartService(Path.Combine(root, "autostart", "displayswitch.desktop"), content);
    }

    public static FileAutostartService ForMacOS(string executablePath, string arguments)
    {
        const string Label = "app.displayswitch.agent";
        var content = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>Label</key>
              <string>{Label}</string>
              <key>ProgramArguments</key>
              <array>
                <string>{SecurityElement.Escape(executablePath)}</string>
                <string>{SecurityElement.Escape(arguments)}</string>
              </array>
              <key>RunAtLoad</key>
              <true/>
            </dict>
            </plist>

            """;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new FileAutostartService(Path.Combine(home, "Library", "LaunchAgents", Label + ".plist"), content);
    }
}
