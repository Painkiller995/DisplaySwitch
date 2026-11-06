using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Settings;
using DisplaySwitch.Core.Shortcuts;

namespace DisplaySwitch.App.ViewModels;

public enum OnboardingStep
{
    Welcome,
    Monitors,
    Inputs,
}

/// <summary>
/// First-run flow: welcome, find monitors, name inputs. Every step can be skipped, and nothing is
/// written until the user finishes.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly bool _shortcutsSupported;
    private readonly Action _completed;

    public OnboardingViewModel(WorkspaceViewModel workspace, ISettingsStore settings, bool shortcutsSupported, Action completed)
    {
        Workspace = workspace;
        _settings = settings;
        _shortcutsSupported = shortcutsSupported;
        _completed = completed;
    }

    public WorkspaceViewModel Workspace { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcome), nameof(IsMonitors), nameof(IsInputs), nameof(PrimaryActionLabel))]
    public partial OnboardingStep Step { get; private set; }

    public bool IsWelcome => Step == OnboardingStep.Welcome;

    public bool IsMonitors => Step == OnboardingStep.Monitors;

    public bool IsInputs => Step == OnboardingStep.Inputs;

    /// <summary>One label for both the visible text and the accessible name, so they never disagree.</summary>
    public string PrimaryActionLabel => Step switch
    {
        OnboardingStep.Welcome => "Get started",
        OnboardingStep.Monitors when !CanNameInputs => "Finish",
        OnboardingStep.Monitors => "Continue",
        _ => "Finish",
    };

    public IReadOnlyList<OnboardingInputViewModel> Inputs { get; private set; } = [];

    public bool CanNameInputs => Workspace.SelectedMonitor is { IsControllable: true, HasInputs: true };

    public bool ShortcutsSupported => _shortcutsSupported;

    [RelayCommand]
    private async Task ContinueAsync()
    {
        switch (Step)
        {
            case OnboardingStep.Welcome:
                Step = OnboardingStep.Monitors;
                if (!Workspace.HasScanned)
                {
                    await Workspace.RefreshAsync(forceCapabilities: false);
                }

                OnPropertyChanged(nameof(CanNameInputs));
                OnPropertyChanged(nameof(PrimaryActionLabel));
                break;

            case OnboardingStep.Monitors when CanNameInputs:
                Inputs = [.. Workspace.SelectedMonitor!.Inputs.Select(card => new OnboardingInputViewModel(card.Source))];
                OnPropertyChanged(nameof(Inputs));
                Step = OnboardingStep.Inputs;
                break;

            default:
                Finish();
                break;
        }
    }

    [RelayCommand]
    private void Back() => Step = Step == OnboardingStep.Inputs ? OnboardingStep.Monitors : OnboardingStep.Welcome;

    [RelayCommand]
    private void Skip()
    {
        _settings.Update(s => s with { OnboardingCompleted = true });
        _completed();
    }

    private void Finish()
    {
        var monitor = Workspace.SelectedMonitor;
        _settings.Update(s =>
        {
            var updated = s with { OnboardingCompleted = true };
            if (monitor is null || Inputs.Count == 0)
            {
                return updated;
            }

            var named = Inputs.ToDictionary(i => i.Source.Code, i => i.ToInputSource());
            var inputs = monitor.ConfiguredInputs.Select(i => named.GetValueOrDefault(i.Code, i)).ToList();
            updated = updated.WithMonitor(monitor.Key, m => m with { Inputs = inputs });

            // Offer Ctrl+Alt+1…9 for visible inputs, without taking a gesture that is already assigned.
            if (_shortcutsSupported && !s.Shortcuts.Any(b => b.MonitorKey == monitor.Key))
            {
                var free = ShortcutRules.DefaultGestures().Where(g => s.Shortcuts.All(b => b.Gesture != g));
                var bindings = inputs.Where(i => !i.IsHidden).Zip(free, (input, gesture) => new ShortcutBinding(gesture, monitor.Key, input.Code));
                updated = updated with { Shortcuts = [.. s.Shortcuts, .. bindings] };
            }

            return updated;
        });
        _completed();
    }
}

/// <summary>One input row on the "name your inputs" step.</summary>
public sealed partial class OnboardingInputViewModel : ObservableObject
{
    public OnboardingInputViewModel(InputSource source)
    {
        Source = source;
        Label = source.Label ?? string.Empty;
        IconChoices = [.. Enum.GetValues<InputIcon>().Select(icon => new IconOption(icon, IconChoiceViewModel.Names[icon]))];
        SelectedIcon = IconChoices.First(c => c.Icon == source.Icon);
    }

    public InputSource Source { get; }

    public string PortName => Source.PortName;

    public IReadOnlyList<IconOption> IconChoices { get; }

    [ObservableProperty]
    public partial string Label { get; set; }

    [ObservableProperty]
    public partial IconOption SelectedIcon { get; set; }

    public InputSource ToInputSource() => Source with
    {
        Label = string.IsNullOrWhiteSpace(Label) ? null : Label.Trim(),
        Icon = SelectedIcon.Icon,
    };
}

/// <summary>An icon with its accessible name, for compact pickers.</summary>
public sealed record IconOption(InputIcon Icon, string Name);
