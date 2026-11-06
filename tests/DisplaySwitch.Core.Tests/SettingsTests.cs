using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Settings;
using DisplaySwitch.Core.Shortcuts;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplaySwitch.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("displayswitch-tests-").FullName;

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void Missing_file_loads_defaults()
    {
        var store = JsonSettingsStore.Load(SettingsPath, NullLogger.Instance);

        Assert.Equal(ThemePreference.System, store.Current.Theme);
        Assert.False(store.Current.OnboardingCompleted);
        Assert.Null(store.LoadNotice);
    }

    [Fact]
    public async Task Changes_round_trip_through_disk()
    {
        var store = JsonSettingsStore.Load(SettingsPath, NullLogger.Instance);
        var gesture = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKey.D2);
        store.Update(s => s
            .WithMonitor("SAM0F21", m => m with
            {
                CustomName = "Desk",
                Inputs = [new InputSource { Code = new InputCode(0x11), PortName = "HDMI 1", Label = "Work laptop", Icon = InputIcon.Laptop, ReadAliases = [new InputCode(0x05)] }],
            }) with
        {
            Theme = ThemePreference.Dark,
            Shortcuts = [new ShortcutBinding(gesture, "SAM0F21", new InputCode(0x11))],
        });
        await store.FlushAsync();

        var reloaded = JsonSettingsStore.Load(SettingsPath, NullLogger.Instance).Current;

        Assert.Equal(ThemePreference.Dark, reloaded.Theme);
        var monitor = reloaded.FindMonitor("sam0f21");
        Assert.NotNull(monitor);
        Assert.Equal("Desk", monitor.CustomName);
        var input = Assert.Single(monitor.Inputs!);
        Assert.Equal("Work laptop", input.DisplayName);
        Assert.Equal(InputIcon.Laptop, input.Icon);
        Assert.Equal([new InputCode(0x05)], input.ReadAliases);
        Assert.Equal(gesture, Assert.Single(reloaded.Shortcuts).Gesture);
        Assert.Contains("\"Ctrl+Alt+2\"", File.ReadAllText(SettingsPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Corrupt_file_is_backed_up_and_reported()
    {
        File.WriteAllText(SettingsPath, "{ this is not json");

        var store = JsonSettingsStore.Load(SettingsPath, NullLogger.Instance);

        Assert.NotNull(store.LoadNotice);
        Assert.False(File.Exists(SettingsPath));
        Assert.Single(Directory.GetFiles(_directory, "settings.json.corrupt-*"));
    }

    [Fact]
    public void Invalid_entries_are_dropped_instead_of_failing_the_load()
    {
        File.WriteAllText(SettingsPath, """
            {
              "theme": "Dark",
              "monitors": [
                { "monitorKey": "A", "inputs": [ { "code": 0, "portName": "bad" }, { "code": 17, "portName": "HDMI 1" }, { "code": 17, "portName": "dupe" } ] },
                { "monitorKey": "" }
              ],
              "shortcuts": [
                { "gesture": "Shift+1", "monitorKey": "A", "input": 17 },
                { "gesture": "Ctrl+Alt+1", "monitorKey": "A", "input": 17 }
              ]
            }
            """);

        var settings = JsonSettingsStore.Load(SettingsPath, NullLogger.Instance).Current;

        var monitor = Assert.Single(settings.Monitors);
        Assert.Equal("HDMI 1", Assert.Single(monitor.Inputs!).PortName);
        Assert.Equal("Ctrl+Alt+1", Assert.Single(settings.Shortcuts).Gesture.ToString());
    }

    [Fact]
    public void Update_raises_changed_with_new_state()
    {
        var store = JsonSettingsStore.Load(SettingsPath, NullLogger.Instance);
        AppSettings? observed = null;
        store.Changed += (_, s) => observed = s;

        store.Update(s => s with { CloseToTray = false });

        Assert.False(observed?.CloseToTray);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

public class InputLayoutTests
{
    private static readonly IReadOnlyList<InputSource> Detected =
    [
        new() { Code = new InputCode(0x0F), PortName = "DisplayPort" },
        new() { Code = new InputCode(0x11), PortName = "HDMI 1" },
    ];

    [Fact]
    public void Uses_detected_inputs_until_customized() =>
        Assert.Same(Detected, InputLayout.Resolve(Detected, new MonitorSettings { MonitorKey = "A" }));

    [Fact]
    public void Hidden_inputs_are_configured_but_not_visible()
    {
        var settings = new MonitorSettings { MonitorKey = "A", Inputs = [Detected[0], Detected[1] with { IsHidden = true }] };

        Assert.Equal(2, InputLayout.Resolve(Detected, settings).Count);
        Assert.Equal("DisplayPort", Assert.Single(InputLayout.Visible(Detected, settings)).PortName);
    }
}

public class HotkeyGestureTests
{
    [Theory]
    [InlineData("Ctrl+Alt+1", "Ctrl+Alt+1")]
    [InlineData("alt + ctrl + f5", "Ctrl+Alt+F5")]
    [InlineData("Win+Shift+H", "Shift+Win+H")]
    [InlineData("Cmd+K", "Win+K")]
    public void Parses_and_formats_canonically(string input, string expected)
    {
        Assert.True(HotkeyGesture.TryParse(input, out var gesture));
        Assert.Equal(expected, gesture.ToString());
    }

    [Theory]
    [InlineData("1")]
    [InlineData("Shift+A")]
    [InlineData("Ctrl+Ctrl+1")]
    [InlineData("Ctrl+Alt+")]
    [InlineData("Ctrl+Alt+Escape")]
    [InlineData("Ctrl+Alt+12")]
    [InlineData("Hyper+1")]
    public void Rejects_gestures_unsuitable_for_global_shortcuts(string input) =>
        Assert.False(HotkeyGesture.TryParse(input, out _));

    [Fact]
    public void Exposes_parts_for_keycaps() =>
        Assert.Equal(["Ctrl", "Alt", "3"], new HotkeyGesture(HotkeyModifiers.Alt | HotkeyModifiers.Control, HotkeyKey.D3).Parts);
}

public class ShortcutRulesTests
{
    private static readonly HotkeyGesture CtrlAlt1 = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKey.D1);
    private static readonly HotkeyGesture CtrlAlt2 = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKey.D2);

    [Fact]
    public void Detects_conflict_with_another_input()
    {
        IReadOnlyList<ShortcutBinding> bindings = [new(CtrlAlt1, "A", new InputCode(0x0F))];

        Assert.NotNull(ShortcutRules.FindConflict(bindings, CtrlAlt1, "A", new InputCode(0x11)));
        Assert.Null(ShortcutRules.FindConflict(bindings, CtrlAlt1, "A", new InputCode(0x0F)));
        Assert.Null(ShortcutRules.FindConflict(bindings, CtrlAlt2, "A", new InputCode(0x11)));
    }

    [Fact]
    public void Assigning_replaces_the_previous_owner_and_the_inputs_old_shortcut()
    {
        IReadOnlyList<ShortcutBinding> bindings = [new(CtrlAlt1, "A", new InputCode(0x0F)), new(CtrlAlt2, "A", new InputCode(0x11))];

        var updated = ShortcutRules.Assign(bindings, CtrlAlt1, "A", new InputCode(0x11));

        var binding = Assert.Single(updated);
        Assert.Equal(CtrlAlt1, binding.Gesture);
        Assert.Equal(new InputCode(0x11), binding.Input);
    }

    [Fact]
    public void Defaults_are_ctrl_alt_digits() =>
        Assert.Equal(["Ctrl+Alt+1", "Ctrl+Alt+2"], ShortcutRules.DefaultGestures().Take(2).Select(g => g.ToString()));
}
