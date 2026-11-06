using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using DisplaySwitch.App.ViewModels;
using DisplaySwitch.Core.Settings;
using DisplaySwitch.TestSupport;

namespace DisplaySwitch.App.Tests;

public class HomeScreenTests
{
    [AvaloniaFact]
    public async Task Shows_the_monitor_its_inputs_and_the_live_input()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()), TestHost.Onboarded);

        var monitor = host.Shell.Workspace.SelectedMonitor;
        Assert.NotNull(monitor);
        Assert.Equal("Samsung C49J89x", monitor.DisplayName);
        Assert.Equal(["DisplayPort", "HDMI"], monitor.Inputs.Select(i => i.DisplayName));
        Assert.Equal("DisplayPort", monitor.ActiveInput?.DisplayName);
        Assert.True(host.Shell.Main.IsHome);
    }

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Renders_in_both_themes(string theme)
    {
        using var host = await TestHost.StartAsync(
            new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()),
            s => TestHost.Onboarded(s) with { Theme = theme == "dark" ? ThemePreference.Dark : ThemePreference.Light });

        Assert.Equal(theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light, host.Window.ActualThemeVariant);
        host.Capture($"home-{theme}");
    }

    [AvaloniaFact]
    public async Task Clicking_a_card_switches_and_confirms_only_after_verification()
    {
        var fake = FakeDdcMonitor.SamsungC49J89x();
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(fake), TestHost.Onboarded);
        var monitor = host.Shell.Workspace.SelectedMonitor!;
        var hdmi1 = monitor.Inputs[1];

        await monitor.SwitchToCommand.ExecuteAsync(hdmi1);
        TestHost.Pump();

        Assert.Equal(0x01, fake.CurrentInput);
        Assert.Equal(FeedbackKind.Success, monitor.Feedback.Current?.Kind);
        Assert.Equal("Now showing HDMI", monitor.Feedback.Current?.Title);
        Assert.Same(hdmi1, monitor.ActiveInput);
        host.Capture("home-after-switch");
    }

    [AvaloniaFact]
    public async Task Ignored_command_is_reported_honestly()
    {
        var fake = FakeDdcMonitor.SamsungC49J89x();
        fake.IgnoredCodes.Add(0x01);
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(fake), TestHost.Onboarded);
        var monitor = host.Shell.Workspace.SelectedMonitor!;

        await monitor.SwitchToCommand.ExecuteAsync(monitor.Inputs[1]);
        TestHost.Pump();

        Assert.Equal(FeedbackKind.Warning, monitor.Feedback.Current?.Kind);
        Assert.Equal("Samsung C49J89x stayed on DisplayPort", monitor.Feedback.Current?.Title);
        Assert.True(monitor.Feedback.CanRetry);
        Assert.Equal("DisplayPort", monitor.ActiveInput?.DisplayName);
        host.Capture("home-not-confirmed");
    }

    [AvaloniaFact]
    public async Task Number_keys_switch_to_the_matching_card()
    {
        var fake = FakeDdcMonitor.SamsungC49J89x();
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(fake), TestHost.Onboarded);

        host.Window.KeyPressQwerty(PhysicalKey.Digit2, RawInputModifiers.None);
        await TestHost.WaitUntilAsync(() => host.Shell.Workspace.SelectedMonitor!.Feedback.Current?.Kind == FeedbackKind.Success);

        Assert.Equal(0x01, fake.CurrentInput);
    }
}
