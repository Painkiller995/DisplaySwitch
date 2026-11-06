using CommunityToolkit.Mvvm.ComponentModel;
using DisplaySwitch.Core.Adjustments;

namespace DisplaySwitch.App.ViewModels;

/// <summary>Brightness and color temperature sliders for one monitor.</summary>
public sealed partial class PictureViewModel(PictureAdjustments adjustments) : ObservableObject
{
    private int _pendingChanges;
    private bool _syncing;

    public FeedbackViewModel Feedback { get; } = new();

    public bool IsAvailable => HasBrightness || HasColorTemperature;

    public bool HasBrightness => adjustments.Brightness is not null;

    public double BrightnessMaximum => adjustments.Brightness?.Maximum ?? 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BrightnessLabel))]
    public partial double Brightness { get; set; }

    public string BrightnessLabel => $"{Math.Round(Brightness / BrightnessMaximum * 100)}%";

    public bool HasColorTemperature => adjustments.ColorTemperature is not null;

    public double ColorTemperatureMaximum => adjustments.ColorTemperature?.Maximum ?? 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorTemperatureLabel))]
    public partial double ColorTemperature { get; set; }

    /// <summary>Kelvin when the monitor reports its scale; otherwise a relative description.</summary>
    public string ColorTemperatureLabel
    {
        get
        {
            if (adjustments.ToKelvin((int)ColorTemperature) is { } kelvin)
            {
                return $"{kelvin:N0} K";
            }

            var position = ColorTemperature / ColorTemperatureMaximum;
            return position switch
            {
                < 1.0 / 3 => "Warm",
                > 2.0 / 3 => "Cool",
                _ => "Neutral",
            };
        }
    }

    /// <summary>Reads the monitor's current values. Skipped while the user is changing one, so the slider never jumps.</summary>
    public async Task RefreshAsync()
    {
        if (_pendingChanges > 0)
        {
            return;
        }

        await adjustments.LoadAsync(CancellationToken.None);
        if (_pendingChanges == 0)
        {
            Sync();
        }
    }

    partial void OnBrightnessChanged(double value)
    {
        if (!_syncing && adjustments.Brightness is { } feature)
        {
            _ = ApplyAsync(feature, value, "brightness");
        }
    }

    partial void OnColorTemperatureChanged(double value)
    {
        if (!_syncing && adjustments.ColorTemperature is { } feature)
        {
            _ = ApplyAsync(feature, value, "color temperature");
        }
    }

    private async Task ApplyAsync(AdjustableFeature feature, double value, string name)
    {
        _pendingChanges++;
        AdjustmentResult result;
        try
        {
            result = await adjustments.SetAsync(feature, (int)Math.Round(value));
        }
        finally
        {
            _pendingChanges--;
        }

        switch (result.Outcome)
        {
            case AdjustmentOutcome.Applied:
                Feedback.Dismiss();
                break;
            case AdjustmentOutcome.NotApplied:
                Sync();
                Feedback.Show(new Feedback(
                    FeedbackKind.Warning,
                    $"The monitor didn't change its {name}",
                    "It accepted the command but kept its setting. The current picture mode may lock it: choose a custom or user mode in the monitor's on-screen menu, then try again."));
                break;
            case AdjustmentOutcome.Failed:
                Sync();
                Feedback.Show(new Feedback(FeedbackKind.Error, $"Couldn't change {name}", "The monitor didn't respond. Try again in a moment."));
                break;
        }
    }

    /// <summary>Shows the monitor's reported values without writing them back.</summary>
    private void Sync()
    {
        _syncing = true;
        try
        {
            // Ranges first: a slider coerces its value to the current maximum, which would echo back as a change.
            OnPropertyChanged(nameof(IsAvailable));
            OnPropertyChanged(nameof(HasBrightness));
            OnPropertyChanged(nameof(BrightnessMaximum));
            OnPropertyChanged(nameof(HasColorTemperature));
            OnPropertyChanged(nameof(ColorTemperatureMaximum));
            Brightness = adjustments.Brightness?.Value ?? 0;
            ColorTemperature = adjustments.ColorTemperature?.Value ?? 0;
            OnPropertyChanged(nameof(ColorTemperatureLabel));
        }
        finally
        {
            _syncing = false;
        }
    }
}
