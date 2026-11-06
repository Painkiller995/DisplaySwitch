using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Monitors;
using DisplaySwitch.Core.Profiles;
using DisplaySwitch.Core.Settings;
using DisplaySwitch.Core.Shortcuts;
using DisplaySwitch.Core.Switching;

namespace DisplaySwitch.App.ViewModels;

/// <summary>One monitor: its identity, its input cards and the outcome of the last switch.</summary>
public sealed partial class MonitorViewModel : ObservableObject
{
    private readonly MonitorController _controller;
    private readonly ISettingsStore _settings;

    public MonitorViewModel(MonitorController controller, ISettingsStore settings)
    {
        _controller = controller;
        _settings = settings;
        Picture = new PictureViewModel(controller.Adjustments);
        Rebuild(settings.Current);
    }

    public MonitorController Controller => _controller;

    public string Key => _controller.Identity.Key;

    public MonitorIdentity Identity => _controller.Identity;

    public MonitorSupport Support => _controller.Support;

    public bool IsControllable => Support == MonitorSupport.Ready;

    /// <summary>The user's name, else the retail name from a profile, else the name the monitor reports.</summary>
    public string DisplayName =>
        _settings.Current.FindMonitor(Key)?.CustomName
        ?? MonitorProfiles.Find(Identity.HardwareId)?.ModelName
        ?? Identity.Name;

    public string Details
    {
        get
        {
            var parts = new List<string>();
            if (Identity.Connection is not ConnectionKind.Unknown)
            {
                parts.Add(ConnectionLabel(Identity.Connection));
            }

            if (Identity.HasResolution)
            {
                parts.Add($"{Identity.Width} × {Identity.Height}");
            }

            return string.Join("  ·  ", parts);
        }
    }

    /// <summary>The shape of the monitor illustration, clamped so extreme values still look like a monitor.</summary>
    public double AspectRatio => Identity.HasResolution ? Math.Clamp((double)Identity.Width / Identity.Height, 1.25, 3.6) : 16.0 / 9.0;

    public string? SupportTitle => Support switch
    {
        MonitorSupport.NoDdc => "This monitor isn't responding to DisplaySwitch",
        MonitorSupport.NoInputControl => "This monitor doesn't allow input switching",
        _ => null,
    };

    public string? SupportMessage => Support switch
    {
        MonitorSupport.NoDdc when Identity.Connection == ConnectionKind.Internal => "Built-in displays don't support external control.",
        MonitorSupport.NoDdc => "Turn on DDC/CI in the monitor's on-screen menu, then refresh. Some docks and adapters don't pass these commands through.",
        MonitorSupport.NoInputControl => "It responds to other commands, but its firmware doesn't offer input selection.",
        _ => null,
    };

    public ObservableCollection<InputCardViewModel> Inputs { get; } = [];

    public bool HasInputs => Inputs.Count > 0;

    public bool ShowNoInputs => IsControllable && !HasInputs;

    /// <summary>Keyboard hint matching the available number keys and global shortcuts.</summary>
    public string KeyboardTip
    {
        get
        {
            var keys = Math.Min(Inputs.Count, 9);
            var numbers = keys == 1 ? "Press 1 to switch" : $"Press 1–{keys} to switch";
            return Inputs.Any(i => i.HasShortcut) ? $"{numbers}. The shortcuts on each card work from any app." : $"{numbers}. Add global shortcuts from each card's edit button.";
        }
    }

    public FeedbackViewModel Feedback { get; } = new();

    public PictureViewModel Picture { get; }

    /// <summary>Whether DDC/CI answers at all, so picture controls are worth probing even without input control.</summary>
    public bool HasDdc => Support != MonitorSupport.NoDdc;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveInput), nameof(ActiveTitle), nameof(ActiveCaption), nameof(ActiveIcon))]
    public partial InputCardViewModel? ActiveInput { get; private set; }

    public bool HasActiveInput => ActiveInput is not null;

    /// <summary>Raised when the monitor reports a different input, so the tray and quick switcher can update.</summary>
    public event EventHandler? ActiveInputChanged;

    public string ActiveTitle => ActiveInput?.DisplayName ?? (IsControllable ? "Input not recognized" : "Not controllable");

    public string? ActiveCaption => ActiveInput?.PortCaption ?? (ActiveInput is null && _controller.ReportedInput is { } code && IsControllable ? $"Monitor reports {code}" : null);

    public InputIcon ActiveIcon => ActiveInput?.Icon ?? InputIcon.Port;

    /// <summary>All configured inputs, hidden ones included: the only codes that may be written.</summary>
    public IReadOnlyList<InputSource> ConfiguredInputs => InputLayout.Resolve(_controller.DetectedInputs, _settings.Current.FindMonitor(Key));

    /// <summary>Rebuilds cards after settings changed, keeping any in-flight switch indicator.</summary>
    public void Rebuild(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var switching = Inputs.Where(i => i.IsSwitching).Select(i => i.Source.Code).ToHashSet();
        var visible = InputLayout.Visible(_controller.DetectedInputs, settings.FindMonitor(Key));

        Inputs.Clear();
        for (var i = 0; i < visible.Count; i++)
        {
            var shortcut = ShortcutRules.Find(settings.Shortcuts, Key, visible[i].Code)?.Gesture;
            Inputs.Add(new InputCardViewModel(visible[i], i + 1, shortcut) { IsSwitching = switching.Contains(visible[i].Code) });
        }

        UpdateActiveInput();
        OnPropertyChanged(nameof(HasInputs));
        OnPropertyChanged(nameof(ShowNoInputs));
        OnPropertyChanged(nameof(KeyboardTip));
        OnPropertyChanged(nameof(DisplayName));
    }

    public async Task RefreshActiveInputAsync(CancellationToken cancellationToken)
    {
        if (IsControllable)
        {
            await _controller.RefreshReportedInputAsync(cancellationToken);
            UpdateActiveInput();
        }
    }

    /// <summary>Switches to an input and reports the outcome in <see cref="Feedback"/>.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task SwitchToAsync(InputCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!IsControllable)
        {
            Feedback.Show(new Feedback(FeedbackKind.Error, SupportTitle ?? "This monitor can't be controlled", SupportMessage));
            return;
        }

        foreach (var input in Inputs)
        {
            input.IsSwitching = input == card;
        }

        Feedback.Show(SwitchFeedback.InProgress(card.Source));
        var configured = ConfiguredInputs;
        var result = await _controller.SwitchAsync(card.Source, configured, CancellationToken.None);
        if (result.Outcome == SwitchOutcome.Superseded)
        {
            return;
        }

        card.IsSwitching = false;
        UpdateActiveInput();
        if (SwitchFeedback.Describe(result, DisplayName, Identity.HardwareId, configured) is { } feedback)
        {
            // Retry targets the current card for this input; settings may have rebuilt the cards meanwhile.
            Feedback.Show(feedback, () => SwitchToAsync(Inputs.FirstOrDefault(i => i.Source.Code == card.Source.Code) ?? card));
        }
    }

    partial void OnActiveInputChanged(InputCardViewModel? value) => ActiveInputChanged?.Invoke(this, EventArgs.Empty);

    private void UpdateActiveInput()
    {
        var reported = _controller.ReportedInput;
        InputCardViewModel? active = null;
        foreach (var input in Inputs)
        {
            input.IsActive = reported is { } code && input.Source.IsReportedAs(code);
            active ??= input.IsActive ? input : null;
        }

        ActiveInput = active;
        OnPropertyChanged(nameof(ActiveCaption));
        OnPropertyChanged(nameof(ActiveTitle));
    }

    private static string ConnectionLabel(ConnectionKind connection) => connection switch
    {
        ConnectionKind.DisplayPort => "DisplayPort",
        ConnectionKind.Hdmi => "HDMI",
        ConnectionKind.Dvi => "DVI",
        ConnectionKind.Vga => "VGA",
        ConnectionKind.Internal => "Built-in",
        ConnectionKind.Wireless => "Wireless",
        _ => "Unknown connection",
    };
}
