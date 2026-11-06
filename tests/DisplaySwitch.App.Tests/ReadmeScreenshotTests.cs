using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using DisplaySwitch.App.Views;
using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Settings;
using DisplaySwitch.Core.Shortcuts;
using DisplaySwitch.TestSupport;

namespace DisplaySwitch.App.Tests;

/// <summary>
/// Renders the screenshots used in README.md from the real UI, around a simulated monitor.
/// They always render (so a broken screen fails the suite); they are written to docs/screenshots
/// only when DISPLAYSWITCH_UPDATE_SCREENSHOTS=1, so a normal test run never modifies the repository.
/// </summary>
public class ReadmeScreenshotTests
{
    /// <summary>Rendered at 2× so images stay sharp on high-DPI screens and when scaled down in the README.</summary>
    private const double RenderScale = 2;

    [AvaloniaTheory]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public async Task Home(ThemePreference theme)
    {
        using var host = await StartCustomizedAsync(theme, height: 880);

        Save(host.Window, $"home-{theme.ToString().ToLowerInvariant()}");
    }

    [AvaloniaFact]
    public async Task Honest_feedback()
    {
        var monitor = FakeDdcMonitor.SamsungC49J89x();
        monitor.IgnoredCodes.Add(0x01);
        using var host = await StartCustomizedAsync(ThemePreference.Light, monitor, height: 780);
        var selected = host.Shell.Workspace.SelectedMonitor!;

        await selected.SwitchToCommand.ExecuteAsync(selected.Inputs[1]);

        Save(host.Window, "feedback");
    }

    [AvaloniaFact]
    public async Task Customize_input()
    {
        using var host = await StartCustomizedAsync(ThemePreference.Light);
        host.Shell.Main.EditInputCommand.Execute(host.Shell.Workspace.SelectedMonitor!.Inputs[1]);

        Save(host.Window, "customize");
    }

    [AvaloniaFact]
    public async Task Quick_switcher()
    {
        using var host = await StartCustomizedAsync(ThemePreference.Dark);
        var quick = new QuickSwitchWindow { DataContext = host.Shell.Workspace };
        quick.Show();
        try
        {
            // Render the content, not the window, so the rounded corners and shadow stay transparent.
            TestHost.Pump();
            var content = (Control)quick.Content!;
            var size = new PixelSize((int)Math.Ceiling(content.Bounds.Width), (int)Math.Ceiling(content.Bounds.Height));
            using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
            bitmap.Render(content);
            Write(bitmap, "quick-switch");
        }
        finally
        {
            quick.Close();
        }
    }

    [AvaloniaFact]
    public async Task Onboarding()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()), s => s with { Theme = ThemePreference.Light });

        Save(host.Window, "onboarding");
    }

    [AvaloniaFact]
    public async Task Settings()
    {
        using var host = await StartCustomizedAsync(ThemePreference.Dark);
        host.Shell.Main.OpenSettingsCommand.Execute(null);

        Save(host.Window, "settings");
    }

    /// <summary>A realistic setup: named inputs with icons and shortcuts, as a user would have after onboarding.</summary>
    private static Task<TestHost> StartCustomizedAsync(ThemePreference theme, FakeDdcMonitor? monitor = null, double height = 640)
    {
        static HotkeyGesture CtrlAlt(HotkeyKey key) => new(HotkeyModifiers.Control | HotkeyModifiers.Alt, key);

        return TestHost.StartAsync(
            new FakeDisplayBackend(monitor ?? FakeDdcMonitor.SamsungC49J89x()),
            s => s.WithMonitor("SAM0F21", m => m with
            {
                Inputs =
                [
                    new InputSource { Code = new InputCode(0x03), PortName = "DisplayPort", Kind = InputKind.DisplayPort, Label = "Desktop PC", Icon = InputIcon.Desktop },
                    new InputSource { Code = new InputCode(0x01), PortName = "HDMI", Kind = InputKind.Hdmi, Label = "Work laptop", Icon = InputIcon.Laptop },
                ],
            }) with
            {
                Theme = theme,
                OnboardingCompleted = true,
                Shortcuts =
                [
                    new ShortcutBinding(CtrlAlt(HotkeyKey.D1), "SAM0F21", new InputCode(0x03)),
                    new ShortcutBinding(CtrlAlt(HotkeyKey.D2), "SAM0F21", new InputCode(0x01)),
                ],
            },
            width: 900,
            height: height);
    }

    private static void Save(TopLevel window, string name)
    {
        window.SetRenderScaling(RenderScale);
        TestHost.Pump();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Write(frame, name);
    }

    private static void Write(Bitmap frame, string name)
    {
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "screenshots", "readme");
        Directory.CreateDirectory(outputDirectory);
        frame.Save(Path.Combine(outputDirectory, name + ".png"), new PngBitmapEncoderOptions());

        if (Environment.GetEnvironmentVariable("DISPLAYSWITCH_UPDATE_SCREENSHOTS") == "1")
        {
            var docs = Path.Combine(FindRepositoryRoot(), "docs", "screenshots");
            Directory.CreateDirectory(docs);
            frame.Save(Path.Combine(docs, name + ".png"), new PngBitmapEncoderOptions());
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DisplaySwitch.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
