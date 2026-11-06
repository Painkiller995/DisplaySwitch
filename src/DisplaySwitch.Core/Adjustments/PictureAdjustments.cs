using DisplaySwitch.Core.Ddc;
using DisplaySwitch.Core.Switching;
using Microsoft.Extensions.Logging;

namespace DisplaySwitch.Core.Adjustments;

/// <summary>Brightness and color temperature of one monitor.</summary>
/// <remarks>
/// Features are probed only when the capabilities string advertises them, or when it is unavailable.
/// Every change is read back, because some monitors accept a command and ignore it (for example
/// when the picture mode locks color temperature).
/// </remarks>
public sealed partial class PictureAdjustments
{
    /// <summary>MCCS base of the color temperature scale: 3000 K + value × increment.</summary>
    private const int ColorTemperatureBaseKelvin = 3000;

    private readonly MonitorChannel _channel;
    private readonly MonitorCapabilities _capabilities;
    private readonly ILogger _logger;
    private readonly Dictionary<byte, CoalescingVcpWriter> _writers = [];
    private readonly Dictionary<byte, long> _latestRequest = [];

    internal PictureAdjustments(MonitorChannel channel, MonitorCapabilities capabilities, ILogger logger)
    {
        _channel = channel;
        _capabilities = capabilities;
        _logger = logger;
    }

    public AdjustableFeature? Brightness { get; private set; }

    public AdjustableFeature? ColorTemperature { get; private set; }

    /// <summary>Kelvin per color temperature step, when the monitor reports it.</summary>
    public int? ColorTemperatureStepKelvin { get; private set; }

    public bool IsLoaded { get; private set; }

    /// <summary>Reads the supported features. Safe to call again to refresh values.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        Brightness = await TryReadAsync(VcpCodes.Brightness, cancellationToken);
        ColorTemperature = await TryReadAsync(VcpCodes.ColorTemperatureRequest, cancellationToken);
        if (ColorTemperature is not null && ColorTemperatureStepKelvin is null
            && await TryReadAsync(VcpCodes.ColorTemperatureIncrement, cancellationToken) is { Value: > 0 } step)
        {
            ColorTemperatureStepKelvin = step.Value;
        }

        IsLoaded = true;
    }

    /// <summary>Converts a color temperature value to Kelvin, or returns <see langword="null"/> if the step is unknown.</summary>
    public int? ToKelvin(int value) => ColorTemperatureStepKelvin is { } step ? ColorTemperatureBaseKelvin + (value * step) : null;

    /// <summary>
    /// Sets a feature (clamped to its range) and reads it back. Rapid calls for the same feature are
    /// coalesced: only the latest value is guaranteed to be written, and earlier calls return
    /// <see cref="AdjustmentOutcome.Superseded"/>.
    /// </summary>
    public async Task<AdjustmentResult> SetAsync(AdjustableFeature feature, int value)
    {
        ArgumentNullException.ThrowIfNull(feature);
        var target = feature.Clamp(value);
        long request;
        lock (_latestRequest)
        {
            request = _latestRequest[feature.Code] = _latestRequest.GetValueOrDefault(feature.Code) + 1;
        }

        try
        {
            await WriterFor(feature.Code).WriteAsync(target);
        }
        catch (Exception ex) when (ex is DdcException or ObjectDisposedException)
        {
            LogSetFailed(_channel.Identity.Key, feature.Code, ex.Message);
            return new AdjustmentResult(AdjustmentOutcome.Failed, Current(feature.Code) ?? feature);
        }

        if (IsSuperseded(feature.Code, request))
        {
            return new AdjustmentResult(AdjustmentOutcome.Superseded, Current(feature.Code) ?? feature);
        }

        var reported = await TryReadAsync(feature.Code, CancellationToken.None);
        if (reported is null)
        {
            return new AdjustmentResult(AdjustmentOutcome.Failed, Current(feature.Code) ?? feature);
        }

        Store(reported);
        return new AdjustmentResult(reported.Value == target ? AdjustmentOutcome.Applied : AdjustmentOutcome.NotApplied, reported);
    }

    private bool IsSuperseded(byte vcpCode, long request)
    {
        lock (_latestRequest)
        {
            return _latestRequest[vcpCode] != request;
        }
    }

    private bool IsAdvertised(byte vcpCode) =>
        _capabilities.VcpFeatures.Count == 0 || _capabilities.VcpFeatures.ContainsKey(vcpCode);

    private async Task<AdjustableFeature?> TryReadAsync(byte vcpCode, CancellationToken cancellationToken)
    {
        if (!IsAdvertised(vcpCode))
        {
            return null;
        }

        // A zero maximum means the feature is not adjustable.
        return await _channel.TryReadVcpAsync(vcpCode, cancellationToken) is { Maximum: > 0 } reading
            ? new AdjustableFeature(vcpCode, Math.Min(reading.Current, reading.Maximum), reading.Maximum)
            : null;
    }

    private AdjustableFeature? Current(byte vcpCode) => vcpCode switch
    {
        VcpCodes.Brightness => Brightness,
        VcpCodes.ColorTemperatureRequest => ColorTemperature,
        _ => null,
    };

    private void Store(AdjustableFeature feature)
    {
        switch (feature.Code)
        {
            case VcpCodes.Brightness:
                Brightness = feature;
                break;
            case VcpCodes.ColorTemperatureRequest:
                ColorTemperature = feature;
                break;
        }
    }

    private CoalescingVcpWriter WriterFor(byte vcpCode)
    {
        lock (_writers)
        {
            if (!_writers.TryGetValue(vcpCode, out var writer))
            {
                _writers[vcpCode] = writer = new CoalescingVcpWriter(_channel, vcpCode);
            }

            return writer;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Setting VCP 0x{Code:X2} on {MonitorKey} failed: {Reason}")]
    private partial void LogSetFailed(string monitorKey, byte code, string reason);
}
