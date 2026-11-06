using System.Globalization;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.App.Services;

/// <summary>
/// Writes a small daily log file for troubleshooting. Logs stay on this computer and contain only
/// technical events (monitor model IDs, DDC results), never personal data. Old files are pruned.
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private const int RetainedFiles = 7;

    private readonly Lock _lock = new();
    private readonly StreamWriter? _writer;

    public FileLoggerProvider(string directory, TimeProvider time)
    {
        try
        {
            Directory.CreateDirectory(directory);
            foreach (var stale in Directory.GetFiles(directory, "displayswitch-*.log").Order().Reverse().Skip(RetainedFiles - 1))
            {
                File.Delete(stale);
            }

            var path = Path.Combine(directory, $"displayswitch-{time.GetLocalNow():yyyyMMdd}.log");
            _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never stop the app from starting; without a writable folder it is simply off.
            _writer = null;
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName[(categoryName.LastIndexOf('.') + 1)..]);

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
        }
    }

    private void Write(string line)
    {
        lock (_lock)
        {
            try
            {
                _writer?.WriteLine(line);
            }
            catch (IOException)
            {
                // Disk full or file removed: drop the line rather than crash.
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information && provider._writer is not null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var line = string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.Now:HH:mm:ss.fff} {logLevel,-11} {category}: {formatter(state, exception)}");
            provider.Write(exception is null ? line : $"{line}{Environment.NewLine}{exception}");
        }
    }
}
