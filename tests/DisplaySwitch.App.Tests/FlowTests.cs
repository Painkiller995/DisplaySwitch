using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using DisplaySwitch.App.Design;
using DisplaySwitch.App.ViewModels;
using DisplaySwitch.App.Views;
using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Monitors;
using DisplaySwitch.Core.Settings;
using DisplaySwitch.Core.Shortcuts;
using DisplaySwitch.Core.Switching;
using DisplaySwitch.TestSupport;

namespace DisplaySwitch.App.Tests;

public class OnboardingTests
{
    [AvaloniaFact]
    public async Task First_run_walks_through_all_steps_and_saves_names_and_shortcuts()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()));
        var main = host.Shell.Main;
        Assert.True(main.IsOnboarding);
        host.Capture("onboarding-1-welcome");

        var onboarding = main.Onboarding!;
        Assert.Equal("Get started", onboarding.PrimaryActionLabel);
        await onboarding.ContinueCommand.ExecuteAsync(null);
        TestHost.Pump();
        Assert.True(onboarding.IsMonitors);
        Assert.Equal("Continue", onboarding.PrimaryActionLabel);
        host.Capture("onboarding-2-monitors");

        await onboarding.ContinueCommand.ExecuteAsync(null);
        TestHost.Pump();
        Assert.True(onboarding.IsInputs);
        onboarding.Inputs[0].Label = "Desktop PC";
        onboarding.Inputs[1].Label = "Work laptop";
        onboarding.Inputs[1].SelectedIcon = onboarding.Inputs[1].IconChoices.First(c => c.Icon == InputIcon.Laptop);
        host.Capture("onboarding-3-inputs");

        await onboarding.ContinueCommand.ExecuteAsync(null);
        TestHost.Pump();

        Assert.True(main.IsHome);
        var settings = host.Settings.Current;
        Assert.True(settings.OnboardingCompleted);
        Assert.Equal(["Desktop PC", "Work laptop"], host.Shell.Workspace.SelectedMonitor!.Inputs.Select(i => i.DisplayName));
        Assert.Equal(["Ctrl+Alt+1", "Ctrl+Alt+2"], settings.Shortcuts.Select(s => s.Gesture.ToString()));
        host.Capture("home-after-onboarding");
    }

    [AvaloniaFact]
    public async Task Skipping_setup_writes_nothing_but_the_completion_flag()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()));

        host.Shell.Main.Onboarding!.SkipCommand.Execute(null);

        Assert.True(host.Shell.Main.IsHome);
        Assert.True(host.Settings.Current.OnboardingCompleted);
        Assert.Empty(host.Settings.Current.Shortcuts);
        Assert.Null(host.Settings.Current.FindMonitor("SAM0F21")?.Inputs);
    }
}

public class InputEditorTests
{
    [AvaloniaFact]
    public async Task Renaming_an_input_and_assigning_a_shortcut_updates_the_card()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()), TestHost.Onboarded);
        var main = host.Shell.Main;
        var monitor = host.Shell.Workspace.SelectedMonitor!;

        main.EditInputCommand.Execute(monitor.Inputs[1]);
        var editor = main.Editor!;
        editor.Label = "PlayStation";
        editor.IconChoices.First(c => c.Icon == InputIcon.GameConsole).IsSelected = true;
        editor.RecordShortcut(new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKey.P));
        host.Capture("editor");
        editor.SaveCommand.Execute(null);
        TestHost.Pump();

        Assert.False(main.IsEditorOpen);
        var card = monitor.Inputs[1];
        Assert.Equal("PlayStation", card.DisplayName);
        Assert.Equal("HDMI", card.PortCaption);
        Assert.Equal(InputIcon.GameConsole, card.Icon);
        Assert.Equal(["Ctrl", "Alt", "P"], card.ShortcutParts);
        Assert.Contains(new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKey.P), host.Hotkeys.Registered);
        host.Capture("home-customized");
    }

    [AvaloniaFact]
    public async Task Shortcut_conflicts_are_explained_before_saving()
    {
        var ctrlAlt1 = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKey.D1);
        using var host = await TestHost.StartAsync(
            new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()),
            s => TestHost.Onboarded(s) with { Shortcuts = [new ShortcutBinding(ctrlAlt1, "SAM0F21", new InputCode(0x03))] });
        var main = host.Shell.Main;

        main.EditInputCommand.Execute(host.Shell.Workspace.SelectedMonitor!.Inputs[1]);
        main.Editor!.RecordShortcut(ctrlAlt1);
        Assert.Equal("Currently used by DisplayPort. Saving moves it to this input.", main.Editor.ShortcutWarning);

        main.Editor.RecordShortcut(new HotkeyGesture(HotkeyModifiers.Shift, HotkeyKey.A));
        Assert.Contains("need Ctrl, Alt", main.Editor.ShortcutWarning, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Shortcut_owned_by_another_app_is_flagged()
    {
        var free = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKey.D2);
        var taken = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKey.D1);
        using var host = await TestHost.StartAsync(
            new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()),
            s => TestHost.Onboarded(s) with { Shortcuts = [new ShortcutBinding(free, "SAM0F21", new InputCode(0x03))] });
        host.Hotkeys.OwnedByOtherApps.Add(taken);
        host.Settings.Update(s => s with { Shortcuts = [new ShortcutBinding(taken, "SAM0F21", new InputCode(0x01))] });
        await TestHost.WaitUntilAsync(() => host.Hotkeys.Registered.Contains(taken));

        host.Shell.Main.EditInputCommand.Execute(host.Shell.Workspace.SelectedMonitor!.Inputs[1]);

        Assert.Contains("Another app is already using this shortcut", host.Shell.Main.Editor!.ShortcutWarning, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Hidden_inputs_leave_the_main_screen_but_stay_configured()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()), TestHost.Onboarded);
        var main = host.Shell.Main;
        var monitor = host.Shell.Workspace.SelectedMonitor!;

        main.EditInputCommand.Execute(monitor.Inputs[1]);
        main.Editor!.IsShownOnHome = false;
        main.Editor.SaveCommand.Execute(null);

        Assert.Equal(["DisplayPort"], monitor.Inputs.Select(i => i.DisplayName));
        Assert.Equal(2, monitor.ConfiguredInputs.Count);
    }

    [AvaloniaFact]
    public async Task Adding_an_input_validates_the_code_and_previews_with_automatic_return()
    {
        var fake = FakeDdcMonitor.SamsungC49J89x();
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(fake), TestHost.Onboarded);
        var main = host.Shell.Main;

        main.AddInputCommand.Execute(null);
        var editor = main.Editor!;
        editor.CodeText = "0x01";
        Assert.Equal("This monitor already has an input with this code.", editor.CodeError);
        editor.CodeText = "300";
        Assert.False(editor.SaveCommand.CanExecute(null));

        editor.CodeText = "0x1B";
        editor.NewPortName = "USB-C";
        editor.Label = "MacBook";
        await editor.PreviewCommand.ExecuteAsync(null);
        Assert.Equal([0x1B, 0x03], fake.Writes);
        Assert.StartsWith("Back on DisplayPort", editor.PreviewStatus, StringComparison.Ordinal);
        host.Capture("editor-add-input");

        editor.SaveCommand.Execute(null);
        var added = host.Shell.Workspace.SelectedMonitor!.Inputs.Last();
        Assert.Equal("MacBook", added.DisplayName);
        Assert.Equal("USB-C", added.PortCaption);
    }
}

public class SettingsAndStatesTests
{
    [AvaloniaFact]
    public async Task Theme_changes_apply_immediately_and_persist()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()), TestHost.Onboarded);
        host.Shell.Main.OpenSettingsCommand.Execute(null);
        var settings = host.Shell.Main.SettingsPage;

        settings.IsDarkTheme = true;
        TestHost.Pump();

        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
        Assert.Equal(ThemePreference.Dark, host.Settings.Current.Theme);
        host.Capture("settings-dark");

        settings.IsLightTheme = true;
        TestHost.Pump();
        host.Capture("settings-light");
        settings.IsSystemTheme = true;
    }

    [AvaloniaFact]
    public async Task Reduce_motion_removes_animation_class()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()), TestHost.Onboarded);
        Assert.Contains("animated", host.Window.Classes);

        host.Shell.Main.SettingsPage.ReduceMotion = true;

        Assert.DoesNotContain("animated", host.Window.Classes);
    }

    [AvaloniaFact]
    public async Task High_contrast_theme_renders()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()), TestHost.Onboarded);
        Application.Current!.RequestedThemeVariant = AppThemes.HighContrast;
        try
        {
            // Wait for theme transitions to settle.
            await Task.Delay(300, TestContext.Current.CancellationToken);
            host.Capture("home-high-contrast");
            Assert.Equal(AppThemes.HighContrast, host.Window.ActualThemeVariant);
        }
        finally
        {
            Application.Current.RequestedThemeVariant = ThemeVariant.Default;
        }
    }

    [AvaloniaFact]
    public async Task No_monitors_shows_a_recoverable_empty_state()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(), TestHost.Onboarded);

        Assert.True(host.Shell.Workspace.ShowEmptyState);
        host.Capture("home-empty");
    }

    [AvaloniaFact]
    public async Task Monitor_without_ddc_explains_how_to_fix_it()
    {
        var silent = new FakeDdcMonitor(identity: new MonitorIdentity("DEL41A8", "DEL41A8", "DELL U2720Q", ConnectionKind.Hdmi, 3840, 2160));
        for (var i = 0; i < 50; i++)
        {
            silent.UpcomingFailures.Enqueue(Core.Ddc.DdcError.NoResponse);
        }

        using var host = await TestHost.StartAsync(new FakeDisplayBackend(silent), TestHost.Onboarded);

        var monitor = host.Shell.Workspace.SelectedMonitor!;
        Assert.Equal(MonitorSupport.NoDdc, monitor.Support);
        Assert.Contains("DDC/CI", monitor.SupportMessage, StringComparison.Ordinal);
        host.Capture("home-unsupported");
    }

    [AvaloniaFact]
    public async Task Multiple_monitors_can_be_selected_and_selection_is_remembered()
    {
        var dell = new FakeDdcMonitor(0x11, identity: new MonitorIdentity("DEL41A8", "DEL41A8", "DELL U2720Q", ConnectionKind.Hdmi, 3840, 2160)) { Capabilities = "(vcp(60(0F 11 12 1B)))" };
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x(), dell), TestHost.Onboarded);
        var workspace = host.Shell.Workspace;

        Assert.True(workspace.HasMultipleMonitors);
        workspace.SelectedMonitor = workspace.Monitors[1];
        TestHost.Pump();

        Assert.Equal("DEL41A8", host.Settings.Current.SelectedMonitorKey);
        Assert.Equal(["DisplayPort 1", "HDMI 1", "HDMI 2", "Input 0x1B"], workspace.SelectedMonitor.Inputs.Select(i => i.DisplayName));
        host.Capture("home-second-monitor");
    }

    [AvaloniaFact]
    public async Task Discovery_errors_are_shown_instead_of_crashing()
    {
        using var host = await TestHost.StartAsync(new FailingBackend(), TestHost.Onboarded);

        Assert.Contains("couldn't list your monitors", host.Shell.Workspace.PlatformProblem, StringComparison.Ordinal);
        Assert.False(host.Shell.Workspace.IsScanning);
    }

    private sealed class FailingBackend : IDisplayBackend
    {
        public DisplayDiscovery Discover() => throw new IOException("Access to /sys/class/drm was denied.");
    }

    [AvaloniaFact]
    public async Task Corrupt_settings_are_reported_after_startup()
    {
        using var host = await TestHost.StartAsync(
            new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()),
            prepareSettingsFile: path => File.WriteAllText(path, "{ not json"));

        Assert.Equal(FeedbackKind.Warning, host.Shell.Main.Notice.Current?.Kind);
        Assert.Equal("Settings were reset", host.Shell.Main.Notice.Current?.Title);
    }

    [AvaloniaFact]
    public async Task Global_shortcut_switches_the_bound_input()
    {
        var fake = FakeDdcMonitor.SamsungC49J89x();
        var gesture = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKey.D2);
        using var host = await TestHost.StartAsync(
            new FakeDisplayBackend(fake),
            s => TestHost.Onboarded(s) with { Shortcuts = [new ShortcutBinding(gesture, "SAM0F21", new InputCode(0x01))] });
        Assert.Contains(gesture, host.Hotkeys.Registered);

        host.Hotkeys.Press(gesture);
        await TestHost.WaitUntilAsync(() => fake.CurrentInput == 0x01 && host.Shell.Workspace.SelectedMonitor!.ActiveInput?.Source.Code == new InputCode(0x01));
    }

    [AvaloniaFact]
    public async Task Quick_switcher_lists_inputs_with_the_live_one_marked()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()), TestHost.Onboarded);
        var quick = new QuickSwitchWindow { DataContext = host.Shell.Workspace };
        quick.Show();
        TestHost.Pump();

        var directory = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(directory);
        quick.CaptureRenderedFrame()?.Save(Path.Combine(directory, "quick-switch.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        quick.Close();
    }

    [AvaloniaFact]
    public async Task Layout_holds_at_minimum_window_size()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()), TestHost.Onboarded, width: 560, height: 520);
        host.Capture("home-minimum-size");
    }

    [AvaloniaFact]
    public async Task Layout_holds_at_150_percent_scaling()
    {
        using var host = await TestHost.StartAsync(new FakeDisplayBackend(FakeDdcMonitor.SamsungC49J89x()), TestHost.Onboarded);
        host.Window.SetRenderScaling(1.5);
        host.Capture("home-scale-150");
    }
}
