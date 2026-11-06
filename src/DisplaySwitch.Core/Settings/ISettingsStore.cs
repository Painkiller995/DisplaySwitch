namespace DisplaySwitch.Core.Settings;

/// <summary>Owns the current <see cref="AppSettings"/> and persists every change.</summary>
public interface ISettingsStore
{
    AppSettings Current { get; }

    /// <summary>A message for the user when loading had to repair or reset the settings file, otherwise <see langword="null"/>.</summary>
    string? LoadNotice { get; }

    /// <summary>Raised on the calling thread after <see cref="Current"/> changes.</summary>
    event EventHandler<AppSettings>? Changed;

    /// <summary>Applies a change atomically and schedules it to be saved.</summary>
    void Update(Func<AppSettings, AppSettings> change);

    /// <summary>Waits until all scheduled saves have reached disk.</summary>
    Task FlushAsync();
}
