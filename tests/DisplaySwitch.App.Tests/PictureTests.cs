using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using DisplaySwitch.App.ViewModels;
using DisplaySwitch.Core.Ddc;
using DisplaySwitch.TestSupport;

namespace DisplaySwitch.App.Tests;

public class PictureTests
{
    [AvaloniaFact]
    public async Task Shows_brightness_and_color_temperature_when_supported()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()), TestHost.Onboarded, height: 900);
        var picture = host.Shell.Workspace.SelectedMonitor!.Picture;

        Assert.True(picture.IsAvailable);
        Assert.Equal(60, picture.Brightness);
        Assert.Equal("60%", picture.BrightnessLabel);
        Assert.Equal(170, picture.ColorTemperatureMaximum);
        Assert.Equal("Warm", picture.ColorTemperatureLabel);
        host.Capture("home-display");
    }

    [AvaloniaFact]
    public async Task Moving_the_brightness_slider_writes_and_verifies_the_value()
    {
        var fake = FakeDdcMonitor.SamsungC49J89x();
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(fake), TestHost.Onboarded);
        var picture = host.Shell.Workspace.SelectedMonitor!.Picture;

        picture.Brightness = 35;
        await TestHost.WaitUntilAsync(() => fake.Features[VcpCodes.Brightness].Current == 35);

        Assert.Equal("35%", picture.BrightnessLabel);
        Assert.False(picture.Feedback.IsVisible);
    }

    [AvaloniaFact]
    public async Task Arrow_keys_adjust_the_focused_slider()
    {
        var fake = FakeDdcMonitor.SamsungC49J89x();
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(fake), TestHost.Onboarded);
        var slider = host.Window.GetVisualDescendants().OfType<Slider>().First(s => AutomationProperties.GetName(s) == "Brightness");

        slider.Focus();
        host.Window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        await TestHost.WaitUntilAsync(() => fake.Features[VcpCodes.Brightness].Current == 61);
    }

    [AvaloniaFact]
    public async Task Ignored_change_snaps_back_and_explains_why()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()), TestHost.Onboarded, height: 960);
        var picture = host.Shell.Workspace.SelectedMonitor!.Picture;

        picture.ColorTemperature = 120;
        await TestHost.WaitUntilAsync(() => picture.Feedback.IsVisible);

        Assert.Equal(40, picture.ColorTemperature);
        Assert.Equal(FeedbackKind.Warning, picture.Feedback.Current?.Kind);
        Assert.Equal("The monitor didn't change its color temperature", picture.Feedback.Current?.Title);
        host.Capture("home-display-not-applied");
    }

    [AvaloniaFact]
    public async Task Failed_change_snaps_back_and_reports_an_error()
    {
        var fake = FakeDdcMonitor.SamsungC49J89x();
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(fake), TestHost.Onboarded);
        var picture = host.Shell.Workspace.SelectedMonitor!.Picture;
        fake.RejectWrites = true;

        picture.Brightness = 10;
        await TestHost.WaitUntilAsync(() => picture.Feedback.IsVisible);

        Assert.Equal(60, picture.Brightness);
        Assert.Equal(FeedbackKind.Error, picture.Feedback.Current?.Kind);
    }

    [AvaloniaFact]
    public async Task Shows_kelvin_when_the_monitor_reports_its_scale()
    {
        var fake = new FakeDdcMonitor(0x0F);
        fake.Features[VcpCodes.ColorTemperatureRequest] = (70, 170);
        fake.Features[VcpCodes.ColorTemperatureIncrement] = (50, 50);
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(fake), TestHost.Onboarded);

        Assert.Equal("6,500 K", host.Shell.Workspace.SelectedMonitor!.Picture.ColorTemperatureLabel);
    }

    [AvaloniaFact]
    public async Task Section_is_hidden_when_the_monitor_has_no_picture_controls()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(new FakeDdcMonitor(0x0F)), TestHost.Onboarded);

        Assert.False(host.Shell.Workspace.SelectedMonitor!.Picture.IsAvailable);
    }
}
