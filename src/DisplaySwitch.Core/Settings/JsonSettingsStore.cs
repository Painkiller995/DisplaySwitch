using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.Core.Settings;

/// <summary>
/// Stores settings as human-readable JSON. Saves run in the background, one at a time, and use
/// write-then-rename so a crash or power loss never leaves a half-written file.
/// </summary>
public sealed partial class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly ILogger _logger;
    private readonly Lock _stateLock = new();
    private Task _saveChain = Task.CompletedTask;
    private AppSettings _current;

    private JsonSettingsStore(string path, AppSettings initial, string? loadNotice, ILogger logger)
    {
        _path = path;
        _current = initial;
        LoadNotice = loadNotice;
        _logger = logger;
    }

    public AppSettings Current
    {
        get
        {
            lock (_stateLock)
            {
                return _current;
            }
        }
    }

    public string? LoadNotice { get; }

    public event EventHandler<AppSettings>? Changed;

    /// <summary>
    /// Loads settings from <paramref name="path"/>. A missing file yields defaults. A corrupt file is
    /// moved aside as a timestamped backup and replaced by defaults, and the user is told.
    /// </summary>
    public static JsonSettingsStore Load(string path, ILogger logger, TimeProvider? time = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(logger);

        if (!File.Exists(path))
        {
            return new JsonSettingsStore(path, new AppSettings(), null, logger);
        }

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize(json, SettingsJsonContext.Readable.AppSettings)
                ?? throw new JsonException("The settings file is empty.");
            return new JsonSettingsStore(path, SettingsSanitizer.Sanitize(loaded), null, logger);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            var timestamp = (time ?? TimeProvider.System).GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var backup = $"{path}.corrupt-{timestamp}";
            File.Move(path, backup, overwrite: true);
            LogCorrupt(logger, backup, ex.Message);
            return new JsonSettingsStore(
                path,
                new AppSettings(),
                $"Your settings file could not be read, so DisplaySwitch started with default settings. A copy of the old file was kept as {Path.GetFileName(backup)}.",
                logger);
        }
    }

    public void Update(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        AppSettings updated;
        lock (_stateLock)
        {
            var previous = _current;
            updated = change(previous);
            if (ReferenceEquals(updated, previous))
            {
                return;
            }

            _current = updated;
            // Chained so saves never overlap and always land in order.
            _saveChain = _saveChain.ContinueWith(_ => Save(updated), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }

        Changed?.Invoke(this, updated);
    }

    public Task FlushAsync()
    {
        lock (_stateLock)
        {
            return _saveChain;
        }
    }

    private void Save(AppSettings settings)
    {
        var temporaryPath = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, SettingsJsonContext.Readable.AppSettings));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // In-memory settings stay authoritative; the next change retries the save.
            LogSaveFailed(_logger, _path, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Settings file was corrupt and has been moved to {BackupPath}: {Reason}")]
    private static partial void LogCorrupt(ILogger logger, string backupPath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not save settings to {Path}: {Reason}")]
    private static partial void LogSaveFailed(ILogger logger, string path, string reason);
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
    /// <summary>
    /// The generated options plus relaxed escaping, so shortcuts such as Ctrl+Alt+1 are written
    /// literally instead of with escaped plus signs. Safe because the file is never embedded in HTML.
    /// </summary>
    /// <remarks>
    /// Created lazily: a static initializer would run before the generator's own static fields
    /// (declared in another part of this partial class) are initialized.
    /// </remarks>
    public static SettingsJsonContext Readable =>
        field ??= new SettingsJsonContext(new JsonSerializerOptions(Default.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
