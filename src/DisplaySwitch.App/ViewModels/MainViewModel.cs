using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplaySwitch.App.Services;
using DisplaySwitch.Core.Settings;

namespace DisplaySwitch.App.ViewModels;

public enum AppPage
{
    Onboarding,
    Home,
    Settings,
}

/// <summary>Navigation and overlays for the main window.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly ShortcutService _shortcuts;

    public MainViewModel(WorkspaceViewModel workspace, SettingsViewModel settingsPage, ISettingsStore settings, ShortcutService shortcuts)
    {
        Workspace = workspace;
        SettingsPage = settingsPage;
        _settings = settings;
        _shortcuts = shortcuts;

        if (settings.Current.OnboardingCompleted)
        {
            Page = AppPage.Home;
        }
        else
        {
            Onboarding = new OnboardingViewModel(workspace, settings, shortcuts.IsSupported, () => NavigateTo(AppPage.Home));
            Page = AppPage.Onboarding;
        }

        if (settings.LoadNotice is { } notice)
        {
            Notice.Show(new Feedback(FeedbackKind.Warning, "Settings were reset", notice));
        }
    }

    public WorkspaceViewModel Workspace { get; }

    public SettingsViewModel SettingsPage { get; }

    public OnboardingViewModel? Onboarding { get; }

    /// <summary>App-level notices that are not about a specific monitor.</summary>
    public FeedbackViewModel Notice { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnboarding), nameof(IsHome), nameof(IsSettings))]
    public partial AppPage Page { get; private set; }

    public bool IsOnboarding => Page == AppPage.Onboarding;

    public bool IsHome => Page == AppPage.Home;

    public bool IsSettings => Page == AppPage.Settings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditorOpen))]
    public partial InputEditorViewModel? Editor { get; private set; }

    public bool IsEditorOpen => Editor is not null;

    [RelayCommand]
    private void OpenSettings() => NavigateTo(AppPage.Settings);

    [RelayCommand]
    private void GoHome() => NavigateTo(AppPage.Home);

    [RelayCommand]
    private void EditInput(InputCardViewModel card)
    {
        if (Workspace.SelectedMonitor is { } monitor)
        {
            Editor = CreateEditor(monitor, card.Source);
        }
    }

    [RelayCommand]
    private void AddInput()
    {
        if (Workspace.SelectedMonitor is { IsControllable: true } monitor)
        {
            Editor = CreateEditor(monitor, null);
        }
    }

    [RelayCommand]
    private void CloseEditor() => Editor = null;

    private InputEditorViewModel CreateEditor(MonitorViewModel monitor, Core.Inputs.InputSource? source) =>
        new(monitor, source, _settings, _shortcuts.IsSupported, _shortcuts.IsTakenBySystem, () => Editor = null);

    private void NavigateTo(AppPage page)
    {
        Editor = null;
        Page = page;
    }
}
