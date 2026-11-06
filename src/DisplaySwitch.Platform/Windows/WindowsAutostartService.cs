using System.Runtime.Versioning;
using DisplaySwitch.Core.Platform;
using Microsoft.Win32;

namespace DisplaySwitch.Platform.Windows;

/// <summary>Starts the app at sign-in through the per-user Run key (no administrator rights needed).</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsAutostartService(string executablePath, string arguments) : IAutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = AppPaths.AppName;

    private string Command => $"\"{executablePath}\" {arguments}";

    public bool IsSupported => true;

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, Command, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
