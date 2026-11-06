using DisplaySwitch.Core.Adjustments;
using DisplaySwitch.Core.Ddc;
using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Monitors;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.Core.Switching;

/// <summary>How far the application can control a monitor.</summary>
public enum MonitorSupport
{
    /// <summary>The monitor answers DDC/CI input queries.</summary>
    Ready,

    /// <summary>The monitor answers DDC/CI but does not implement input selection.</summary>
    NoInputControl,

    /// <summary>
    /// The monitor did not answer DDC/CI. It may be disabled in the monitor menu, or the connection
    /// (some docks, adapters and built-in panels) may not carry it.
    /// </summary>
    NoDdc,
}

/// <summary>
/// Controls input selection for one monitor: reads the active input and switches inputs, then
/// verifies the result.
/// </summary>
public sealed partial class MonitorController : IDisposable
{
    private readonly MonitorChannel _channel;
    private readonly DdcTiming _timing;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly Lock _switchLock = new();
    private CancellationTokenSource? _pendingSwitch;

    public MonitorController(
        MonitorChannel channel,
        MonitorSupport support,
        MonitorCapabilities capabilities,
        IReadOnlyList<InputSource> detectedInputs,
        InputCode? reportedInput,
        DdcTiming timing,
        ILogger logger,
        TimeProvider? time = null)
    {
        _channel = channel;
        Support = support;
        DetectedInputs = detectedInputs;
        Adjustments = new PictureAdjustments(channel, capabilities, logger);
        ReportedInput = reportedInput;
        _timing = timing;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public MonitorIdentity Identity => _channel.Identity;

    public MonitorSupport Support { get; }

    /// <summary>Brightness and color temperature; load with <see cref="PictureAdjustments.LoadAsync"/>.</summary>
    public PictureAdjustments Adjustments { get; }

    /// <summary>Inputs found through a built-in profile or the capabilities string, before user customization.</summary>
    public IReadOnlyList<InputSource> DetectedInputs { get; }

    /// <summary>The input value the monitor most recently reported, if any.</summary>
    public InputCode? ReportedInput { get; private set; }

    public async Task<InputCode?> RefreshReportedInputAsync(CancellationToken cancellationToken)
    {
        var reported = await _channel.TryReadInputAsync(cancellationToken);
        if (reported is not null)
        {
            ReportedInput = reported;
        }

        return reported;
    }

    /// <summary>
    /// Switches to <paramref name="target"/> and verifies the result. A newer call on the same monitor
    /// supersedes an older one that is still verifying, so rapid repeated requests settle on the last.
    /// </summary>
    /// <param name="target">The input to select.</param>
    /// <param name="configuredInputs">The inputs the user can choose from. Writes are limited to these codes.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<SwitchResult> SwitchAsync(InputSource target, IReadOnlyList<InputSource> configuredInputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(configuredInputs);

        if (!target.Code.IsValid || !configuredInputs.Any(i => i.Code == target.Code))
        {
            LogRejected(Identity.Key, target.Code);
            return new SwitchResult(SwitchOutcome.Rejected, target);
        }

        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_switchLock)
        {
            _pendingSwitch?.Cancel();
            _pendingSwitch = request;
        }

        try
        {
            return await SwitchCoreAsync(target, request.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SwitchResult(SwitchOutcome.Superseded, target);
        }
        finally
        {
            lock (_switchLock)
            {
                if (_pendingSwitch == request)
                {
                    _pendingSwitch = null;
                }
            }
        }
    }

    /// <summary>
    /// Briefly selects an unverified input code so the user can see what it does, then returns to
    /// <paramref name="returnTo"/>. Used when the user adds an input code by hand.
    /// </summary>
    /// <returns>Whether the monitor reported being back on <paramref name="returnTo"/> afterwards.</returns>
    /// <exception cref="DdcException">The monitor rejected the test command.</exception>
    public async Task<bool> PreviewInputAsync(InputCode candidate, InputSource returnTo, TimeSpan dwell, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(returnTo);
        LogPreview(Identity.Key, candidate, returnTo.Code);

        await _channel.WriteInputAsync(candidate, cancellationToken);
        try
        {
            await Task.Delay(dwell, _time, cancellationToken);
        }
        finally
        {
            // Always try to return, even when cancelled, so the user is never stranded on another input.
            await _channel.WriteInputAsync(returnTo.Code, CancellationToken.None);
        }

        var outcome = await VerifyAsync(returnTo, CancellationToken.None);
        return outcome.IsConfirmedSuccess;
    }

    public void Dispose() => _channel.Dispose();

    private async Task<SwitchResult> SwitchCoreAsync(InputSource target, CancellationToken cancellationToken)
    {
        var before = await RefreshReportedInputAsync(cancellationToken);
        if (before is { } current && target.IsReportedAs(current))
        {
            LogSwitchResult(Identity.Key, target.Code, SwitchOutcome.AlreadyActive, current);
            return new SwitchResult(SwitchOutcome.AlreadyActive, target, current);
        }

        try
        {
            LogSwitching(Identity.Key, target.Code, before);
            await _channel.WriteInputAsync(target.Code, cancellationToken);
        }
        catch (DdcException ex)
        {
            LogSwitchFailed(Identity.Key, target.Code, ex.Message);
            return new SwitchResult(SwitchOutcome.Failed, target, before, ex.Message);
        }
        catch (ObjectDisposedException)
        {
            return new SwitchResult(SwitchOutcome.Failed, target, before, "The monitor was reconnected while switching.");
        }

        var result = await VerifyAsync(target, cancellationToken);
        LogSwitchResult(Identity.Key, target.Code, result.Outcome, result.ReportedInput);
        return result;
    }

    private async Task<SwitchResult> VerifyAsync(InputSource target, CancellationToken cancellationToken)
    {
        await Task.Delay(_timing.SwitchSettleDelay, _time, cancellationToken);
        var deadline = _time.GetTimestamp() + (long)(_timing.VerifyWindow.TotalSeconds * _time.TimestampFrequency);
        InputCode? lastReported = null;

        while (true)
        {
            var reported = await RefreshReportedInputAsync(cancellationToken);
            if (reported is { } value)
            {
                if (target.IsReportedAs(value))
                {
                    return new SwitchResult(SwitchOutcome.Verified, target, value);
                }

                lastReported = value;
            }

            if (_time.GetTimestamp() >= deadline)
            {
                return lastReported is null
                    ? new SwitchResult(SwitchOutcome.SentWithoutConfirmation, target)
                    : new SwitchResult(SwitchOutcome.NotConfirmed, target, lastReported);
            }

            await Task.Delay(_timing.VerifyPollInterval, _time, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refused to write input {Code} to {MonitorKey}: not a configured input")]
    private partial void LogRejected(string monitorKey, InputCode code);

    [LoggerMessage(Level = LogLevel.Information, Message = "Switching {MonitorKey} to {Code} (monitor reported {Before})")]
    private partial void LogSwitching(string monitorKey, InputCode code, InputCode? before);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Switching {MonitorKey} to {Code} failed: {Reason}")]
    private partial void LogSwitchFailed(string monitorKey, InputCode code, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Switch of {MonitorKey} to {Code} finished as {Outcome} (reported {Reported})")]
    private partial void LogSwitchResult(string monitorKey, InputCode code, SwitchOutcome outcome, InputCode? reported);

    [LoggerMessage(Level = LogLevel.Information, Message = "Previewing input {Candidate} on {MonitorKey}, returning to {ReturnTo}")]
    private partial void LogPreview(string monitorKey, InputCode candidate, InputCode returnTo);
}
