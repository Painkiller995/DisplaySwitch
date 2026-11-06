using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplaySwitch.Core.Ddc;
using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Settings;
using DisplaySwitch.Core.Shortcuts;

namespace DisplaySwitch.App.ViewModels;

/// <summary>Edits one input's name, icon, visibility and shortcut, or adds a new input by code.</summary>
public sealed partial class InputEditorViewModel : ObservableObject
{
    /// <summary>Long enough to recognize the source; the monitor is always switched back afterwards.</summary>
    private static readonly TimeSpan PreviewDuration = TimeSpan.FromSeconds(6);

    private readonly MonitorViewModel _monitor;
    private readonly InputSource? _existing;
    private readonly ISettingsStore _settings;
    private readonly Func<HotkeyGesture, bool> _isTakenBySystem;
    private readonly Action _close;

    public InputEditorViewModel(
        MonitorViewModel monitor,
        InputSource? existing,
        ISettingsStore settings,
        bool shortcutsSupported,
        Func<HotkeyGesture, bool> isTakenBySystem,
        Action close)
    {
        _monitor = monitor;
        _existing = existing;
        _settings = settings;
        _isTakenBySystem = isTakenBySystem;
        _close = close;
        ShortcutsSupported = shortcutsSupported;

        Label = existing?.Label ?? string.Empty;
        IsShownOnHome = existing?.IsHidden != true;
        Shortcut = existing is null ? null : ShortcutRules.Find(settings.Current.Shortcuts, monitor.Key, existing.Code)?.Gesture;
        IconChoices = IconChoiceViewModel.CreateAll(existing?.Icon ?? InputIcon.Port, SelectIcon);
        SelectedIcon = existing?.Icon ?? InputIcon.Port;
    }

    public bool IsNew => _existing is null;

    public string Title => IsNew ? "Add an input" : $"Customize {_existing!.PortName}";

    public string MonitorName => _monitor.DisplayName;

    public string? PortName => _existing?.PortName;

    public string? CodeCaption => _existing is null ? null : $"Input code {_existing.Code}";

    public bool ShortcutsSupported { get; }

    public IReadOnlyList<IconChoiceViewModel> IconChoices { get; }

    [ObservableProperty]
    public partial InputIcon SelectedIcon { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Label { get; set; }

    [ObservableProperty]
    public partial bool IsShownOnHome { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortcutParts), nameof(HasShortcut), nameof(ShortcutWarning))]
    public partial HotkeyGesture? Shortcut { get; private set; }

    public IReadOnlyList<string> ShortcutParts => Shortcut?.Parts ?? [];

    public bool HasShortcut => Shortcut is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortcutWarning))]
    public partial string? ShortcutHint { get; private set; }

    /// <summary>Explains a conflict before saving, so nothing is silently reassigned.</summary>
    public string? ShortcutWarning
    {
        get
        {
            if (ShortcutHint is not null)
            {
                return ShortcutHint;
            }

            if (Shortcut is not { } gesture)
            {
                return null;
            }

            if (_isTakenBySystem(gesture))
            {
                return "Another app is already using this shortcut, so it won't work until that app releases it.";
            }

            var code = _existing?.Code ?? (InputCode.TryParse(CodeText, out var parsed) ? parsed : default);
            var conflict = ShortcutRules.FindConflict(_settings.Current.Shortcuts, gesture, _monitor.Key, code);
            var owner = conflict is null ? null : _monitor.ConfiguredInputs.FirstOrDefault(i => i.Code == conflict.Input)?.DisplayName ?? "another input";
            return owner is null ? null : $"Currently used by {owner}. Saving moves it to this input.";
        }
    }

    // Used only when adding an input.

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(PreviewCommand))]
    [NotifyPropertyChangedFor(nameof(CodeError))]
    public partial string CodeText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string NewPortName { get; set; } = string.Empty;

    public string? CodeError
    {
        get
        {
            if (!IsNew || string.IsNullOrWhiteSpace(CodeText))
            {
                return null;
            }

            if (!InputCode.TryParse(CodeText, out var code))
            {
                return "Enter a number from 1 to 255, or hexadecimal such as 0x11.";
            }

            return _monitor.ConfiguredInputs.Any(i => i.Code == code) ? "This monitor already has an input with this code." : null;
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand), nameof(SaveCommand))]
    public partial bool IsPreviewing { get; private set; }

    [ObservableProperty]
    public partial string? PreviewStatus { get; private set; }

    public bool CanDelete => !IsNew && _monitor.ConfiguredInputs.Count > 1;

    /// <summary>Keeps exactly one icon selected, whichever way the selection changed.</summary>
    private void SelectIcon(InputIcon icon)
    {
        SelectedIcon = icon;
        foreach (var choice in IconChoices ?? [])
        {
            choice.IsSelected = choice.Icon == icon;
        }
    }

    /// <summary>Called by the shortcut recorder with each gesture the user presses.</summary>
    public void RecordShortcut(HotkeyGesture gesture)
    {
        if (!gesture.IsValid)
        {
            ShortcutHint = "Shortcuts need Ctrl, Alt or the Windows/Command key, plus a letter, number or F-key.";
            return;
        }

        ShortcutHint = null;
        Shortcut = gesture;
    }

    [RelayCommand]
    private void ClearShortcut()
    {
        ShortcutHint = null;
        Shortcut = null;
    }

    private bool CanPreview() => IsNew && !IsPreviewing && CodeError is null && InputCode.TryParse(CodeText, out _) && _monitor.IsControllable;

    /// <summary>Shows the candidate input for a few seconds, then always returns to the current input.</summary>
    [RelayCommand(CanExecute = nameof(CanPreview))]
    private async Task PreviewAsync()
    {
        if (!InputCode.TryParse(CodeText, out var candidate))
        {
            return;
        }

        var returnTo = _monitor.ActiveInput?.Source ?? (_monitor.ConfiguredInputs is [var first, ..] ? first : null);
        if (returnTo is null)
        {
            PreviewStatus = "Switch to one of this monitor's inputs first, so DisplaySwitch knows where to return.";
            return;
        }

        IsPreviewing = true;
        PreviewStatus = $"Watch your monitor: it will show input {candidate} for {PreviewDuration.TotalSeconds:0} seconds, then return to {returnTo.DisplayName}.";
        try
        {
            var returned = await _monitor.Controller.PreviewInputAsync(candidate, returnTo, PreviewDuration, CancellationToken.None);
            PreviewStatus = returned
                ? $"Back on {returnTo.DisplayName}. If the monitor showed the device you expected, give it a name and save."
                : $"The monitor didn't confirm it returned to {returnTo.DisplayName}. If the screen is blank, use the monitor's Source button.";
        }
        catch (DdcException ex)
        {
            PreviewStatus = $"The monitor rejected the test: {ex.Message}";
        }
        catch (ObjectDisposedException)
        {
            PreviewStatus = "The monitor was reconnected during the test. Close this dialog and try again.";
        }
        finally
        {
            IsPreviewing = false;
        }
    }

    private bool CanSave() => !IsPreviewing && (!IsNew || (CodeError is null && InputCode.TryParse(CodeText, out _)));

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var baseline = _existing ?? CreateNewInput();
        var updated = baseline with
        {
            Label = string.IsNullOrWhiteSpace(Label) ? null : Label.Trim(),
            Icon = SelectedIcon,
            IsHidden = !IsShownOnHome,
        };

        var inputs = _monitor.ConfiguredInputs.ToList();
        var index = inputs.FindIndex(i => i.Code == updated.Code);
        if (index >= 0)
        {
            inputs[index] = updated;
        }
        else
        {
            inputs.Add(updated);
        }

        var key = _monitor.Key;
        _settings.Update(s => s.WithMonitor(key, m => m with { Inputs = inputs }) with
        {
            Shortcuts = Shortcut is { } gesture
                ? ShortcutRules.Assign(s.Shortcuts, gesture, key, updated.Code)
                : ShortcutRules.Remove(s.Shortcuts, key, updated.Code),
        });
        _close();
    }

    [RelayCommand]
    private void Delete()
    {
        if (_existing is null)
        {
            return;
        }

        var key = _monitor.Key;
        var code = _existing.Code;
        var inputs = _monitor.ConfiguredInputs.Where(i => i.Code != code).ToList();
        _settings.Update(s => s.WithMonitor(key, m => m with { Inputs = inputs }) with { Shortcuts = ShortcutRules.Remove(s.Shortcuts, key, code) });
        _close();
    }

    [RelayCommand]
    private void Cancel() => _close();

    private InputSource CreateNewInput()
    {
        if (!InputCode.TryParse(CodeText, out var code))
        {
            throw new InvalidOperationException("Save is only enabled for a valid input code.");
        }

        var described = MccsInputCatalog.Describe(code);
        return described with
        {
            PortName = string.IsNullOrWhiteSpace(NewPortName) ? described.PortName : NewPortName.Trim(),
        };
    }

    partial void OnCodeTextChanged(string value) => OnPropertyChanged(nameof(ShortcutWarning));
}
