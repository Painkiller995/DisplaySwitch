using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplaySwitch.Core.Settings;
using DisplaySwitch.Core.Shortcuts;
using DisplaySwitch.Core.Switching;

namespace DisplaySwitch.App.ViewModels;

/// <summary>
/// The set of attached monitors and which one is selected. Shared by the main window, the quick
/// switcher, the tray menu and global shortcuts so they always agree.
/// </summary>
public sealed partial class WorkspaceViewModel : ObservableObject
{
    private readonly MonitorDirectory _directory;
    private readonly ISettingsStore _settings;

    public WorkspaceViewModel(MonitorDirectory directory, ISettingsStore settings)
    {
        _directory = directory;
        _settings = settings;
        _settings.Changed += (_, updated) =>
        {
            foreach (var monitor in Monitors)
            {
                monitor.Rebuild(updated);
            }

            MonitorsChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>Raised when the monitor list, a monitor's inputs or the active input changes.</summary>
    public event EventHandler? MonitorsChanged;

    public ObservableCollection<MonitorViewModel> Monitors { get; } = [];

    public bool HasMonitors => Monitors.Count > 0;

    public bool HasMultipleMonitors => Monitors.Count > 1;

    [ObservableProperty]
    public partial MonitorViewModel? SelectedMonitor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    public partial bool IsScanning { get; private set; }

    [ObservableProperty]
    public partial string? PlatformProblem { get; private set; }

    /// <summary>Whether a scan has finished at least once, so the empty state never flashes during startup.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    public partial bool HasScanned { get; private set; }

    public bool ShowEmptyState => HasScanned && !IsScanning && !HasMonitors;

    /// <summary>Rediscovers monitors.</summary>
    /// <param name="forceCapabilities">Re-query capabilities strings instead of using the cache.</param>
    public async Task RefreshAsync(bool forceCapabilities)
    {
        if (IsScanning)
        {
            return;
        }

        IsScanning = true;
        try
        {
            try
            {
                await _directory.RefreshAsync(forceCapabilities, CancellationToken.None);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // Shown to the user (with a retry button) rather than crashing; previous monitors stay listed.
                PlatformProblem = $"DisplaySwitch couldn't list your monitors: {ex.Message}";
                return;
            }

            var previousKey = SelectedMonitor?.Key ?? _settings.Current.SelectedMonitorKey;
            Monitors.Clear();
            foreach (var controller in _directory.Monitors)
            {
                var monitor = new MonitorViewModel(controller, _settings);
                monitor.ActiveInputChanged += (_, _) => MonitorsChanged?.Invoke(this, EventArgs.Empty);
                Monitors.Add(monitor);
            }

            SelectedMonitor = Monitors.FirstOrDefault(m => m.Key == previousKey)
                ?? Monitors.FirstOrDefault(m => m.IsControllable)
                ?? Monitors.FirstOrDefault();
            PlatformProblem = _directory.Problem;
            PictureLoad = LoadPicturesAsync([.. Monitors]);
        }
        finally
        {
            IsScanning = false;
            HasScanned = true;
            OnPropertyChanged(nameof(HasMonitors));
            OnPropertyChanged(nameof(HasMultipleMonitors));
            OnPropertyChanged(nameof(ShowEmptyState));
            MonitorsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => RefreshAsync(forceCapabilities: false);

    /// <summary>Re-reads each monitor's active input and picture values without a full rediscovery.</summary>
    public async Task RefreshActiveInputsAsync()
    {
        if (IsScanning)
        {
            return;
        }

        foreach (var monitor in Monitors.ToList())
        {
            await monitor.RefreshActiveInputAsync(CancellationToken.None);
            if (monitor.HasDdc)
            {
                await monitor.Picture.RefreshAsync();
            }
        }
    }

    /// <summary>
    /// Completes when picture controls have been read for every monitor. Runs after discovery so the
    /// extra DDC reads never delay showing inputs.
    /// </summary>
    public Task PictureLoad { get; private set; } = Task.CompletedTask;

    private static async Task LoadPicturesAsync(IReadOnlyList<MonitorViewModel> monitors)
    {
        foreach (var monitor in monitors.Where(m => m.HasDdc))
        {
            await monitor.Picture.RefreshAsync();
        }
    }

    /// <summary>Performs the switch bound to a global shortcut.</summary>
    /// <returns>The monitor that handled it and its feedback, or <see langword="null"/> if the binding is stale.</returns>
    public async Task<(MonitorViewModel Monitor, Feedback? Feedback)?> SwitchByShortcutAsync(ShortcutBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var monitor = Monitors.FirstOrDefault(m => string.Equals(m.Key, binding.MonitorKey, StringComparison.OrdinalIgnoreCase));
        var card = monitor?.Inputs.FirstOrDefault(i => i.Source.Code == binding.Input);
        if (monitor is null || card is null)
        {
            return null;
        }

        await monitor.SwitchToAsync(card);
        return (monitor, monitor.Feedback.Current);
    }

    partial void OnSelectedMonitorChanged(MonitorViewModel? value)
    {
        if (value is not null && value.Key != _settings.Current.SelectedMonitorKey)
        {
            _settings.Update(s => s with { SelectedMonitorKey = value.Key });
        }
    }
}
