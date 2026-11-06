using DisplaySwitch.Core.Ddc;
using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Monitors;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.Core.Switching;

/// <summary>
/// Gives asynchronous, serialized, retried and time-limited access to one monitor's DDC/CI link.
/// </summary>
/// <remarks>
/// DDC/CI is a slow, half-duplex bus and cannot interleave requests, so every call holds an exclusive
/// gate. A native call that hangs cannot be aborted. On timeout the caller is released, but the gate
/// stays held until the call returns, so a stuck request never overlaps with the next one.
/// </remarks>
public sealed partial class MonitorChannel : IDisposable
{
    private readonly IDdcMonitor _monitor;
    private readonly DdcTiming _timing;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _time;
    private bool _disposed;

    public MonitorChannel(IDdcMonitor monitor, DdcTiming timing, ILogger logger, TimeProvider? time = null)
    {
        _monitor = monitor;
        _timing = timing;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public MonitorIdentity Identity => _monitor.Identity;

    public bool SupportsDdc => _monitor.SupportsDdc;

    /// <summary>Reads the active input, or returns <see langword="null"/> if the monitor gave no usable answer.</summary>
    public async Task<InputCode?> TryReadInputAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await WithRetriesAsync(
                "read input",
                () =>
                {
                    var code = InputCode.FromVcpValue(_monitor.ReadVcp(VcpCodes.InputSelect).Current);
                    // 0 is reserved by MCCS; treat it as a garbled reply.
                    return code.IsValid ? code : throw new DdcException(DdcError.InvalidReply, "The monitor reported input 0.");
                },
                _timing.CommandTimeout,
                cancellationToken);
        }
        catch (DdcException ex)
        {
            LogReadFailed(Identity.Key, ex.Error, ex.Message);
            return null;
        }
        catch (ObjectDisposedException)
        {
            // Channel retired after rediscovery.
            return null;
        }
    }

    /// <exception cref="DdcException">The monitor did not accept the command after all retries.</exception>
    /// <exception cref="ObjectDisposedException">The channel was retired because the monitor was rediscovered.</exception>
    public Task WriteInputAsync(InputCode code, CancellationToken cancellationToken)
    {
        if (!code.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Input code 0 is reserved and must never be written.");
        }

        return WriteVcpAsync(VcpCodes.InputSelect, code.Value, cancellationToken);
    }

    /// <summary>Reads a VCP feature, or returns <see langword="null"/> if it is unsupported or the monitor gave no answer.</summary>
    public async Task<VcpReading?> TryReadVcpAsync(byte vcpCode, CancellationToken cancellationToken)
    {
        try
        {
            return await WithRetriesAsync($"read VCP 0x{vcpCode:X2}", () => _monitor.ReadVcp(vcpCode), _timing.CommandTimeout, cancellationToken);
        }
        catch (DdcException ex)
        {
            LogReadFailed(Identity.Key, ex.Error, ex.Message);
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    /// <exception cref="DdcException">The monitor did not accept the command after all retries.</exception>
    /// <exception cref="ObjectDisposedException">The channel was retired because the monitor was rediscovered.</exception>
    public Task WriteVcpAsync(byte vcpCode, ushort value, CancellationToken cancellationToken) =>
        WithRetriesAsync<object?>(
            $"write VCP 0x{vcpCode:X2}",
            () =>
            {
                _monitor.WriteVcp(vcpCode, value);
                return null;
            },
            _timing.CommandTimeout,
            cancellationToken);

    /// <summary>Reads the raw capabilities string, or returns <see langword="null"/> if unavailable.</summary>
    public async Task<string?> TryReadCapabilitiesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await WithRetriesAsync("read capabilities", _monitor.ReadCapabilities, _timing.CapabilitiesTimeout, cancellationToken);
        }
        catch (DdcException ex)
        {
            LogReadFailed(Identity.Key, ex.Error, ex.Message);
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // Wait for an in-flight native call so the handle is never released while in use.
        _gate.Wait();
        _monitor.Dispose();
        _gate.Dispose();
    }

    private async Task<T> WithRetriesAsync<T>(string operation, Func<T> action, TimeSpan timeout, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await RunExclusiveAsync(action, timeout, cancellationToken);
            }
            catch (DdcException ex) when (ex.IsTransient && attempt < _timing.MaxAttempts)
            {
                LogRetrying(operation, Identity.Key, attempt, ex.Message);
                await Task.Delay(_timing.RetryDelayFor(attempt), _time, cancellationToken);
            }
        }
    }

    private async Task<T> RunExclusiveAsync<T>(Func<T> action, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);

        Task<T> work;
        try
        {
            // The gate is released by the work item itself, not by the awaiting caller (see remarks).
            work = Task.Run(() =>
            {
                try
                {
                    return action();
                }
                finally
                {
                    _gate.Release();
                }
            }, CancellationToken.None);
        }
        catch
        {
            _gate.Release();
            throw;
        }

        try
        {
            return await work.WaitAsync(timeout, _time, cancellationToken);
        }
        catch (TimeoutException ex)
        {
            ObserveAbandoned(work);
            throw new DdcException(DdcError.Timeout, "The monitor did not respond in time.", ex);
        }
        catch (OperationCanceledException)
        {
            ObserveAbandoned(work);
            throw;
        }
    }

    /// <summary>Keeps a late failure of an abandoned native call from surfacing as an unobserved task exception.</summary>
    private void ObserveAbandoned(Task work) =>
        work.ContinueWith(
            t => LogAbandonedFailed(Identity.Key, t.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Retrying {Operation} on {MonitorKey} after attempt {Attempt} failed: {Reason}")]
    private partial void LogRetrying(string operation, string monitorKey, int attempt, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "DDC read on {MonitorKey} failed ({Error}): {Reason}")]
    private partial void LogReadFailed(string monitorKey, DdcError error, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "A timed-out DDC call on {MonitorKey} later failed")]
    private partial void LogAbandonedFailed(string monitorKey, Exception? exception);
}
