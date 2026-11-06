using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DisplaySwitch.App.ViewModels;

/// <summary>The status banner. Shows one <see cref="Feedback"/> at a time; newer feedback replaces older.</summary>
public sealed partial class FeedbackViewModel : ObservableObject
{
    /// <summary>Long enough to read a short sentence twice (WCAG 2.2.1 allows dismissal at any time).</summary>
    private static readonly TimeSpan AutoDismissDelay = TimeSpan.FromSeconds(5);

    private Func<Task>? _retry;

    [ObservableProperty]
    public partial Feedback? Current { get; private set; }

    public bool IsVisible => Current is not null;

    public bool CanRetry => Current?.OfferRetry == true && _retry is not null;

    public void Show(Feedback feedback, Func<Task>? retry = null)
    {
        _retry = retry;
        Current = feedback;
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(CanRetry));

        if (feedback.DismissesAutomatically)
        {
            _ = DismissLaterAsync(feedback);
        }
    }

    [RelayCommand]
    public void Dismiss()
    {
        _retry = null;
        Current = null;
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(CanRetry));
    }

    [RelayCommand]
    private Task RetryAsync() => _retry?.Invoke() ?? Task.CompletedTask;

    /// <summary>Dismisses <paramref name="feedback"/> after a delay unless newer feedback has replaced it.</summary>
    private async Task DismissLaterAsync(Feedback feedback)
    {
        await Task.Delay(AutoDismissDelay);
        if (ReferenceEquals(Current, feedback))
        {
            Dismiss();
        }
    }
}
