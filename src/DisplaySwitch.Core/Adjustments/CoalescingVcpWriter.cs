using DisplaySwitch.Core.Switching;

namespace DisplaySwitch.Core.Adjustments;

/// <summary>
/// Writes one VCP feature with "latest value wins" semantics. A slider produces values far faster
/// than DDC/CI can apply them; intermediate values are dropped and at most one write is in flight.
/// </summary>
internal sealed class CoalescingVcpWriter(MonitorChannel channel, byte vcpCode)
{
    private readonly Lock _lock = new();
    private ushort? _pending;
    private Task? _drain;

    /// <summary>Queues <paramref name="value"/>. The task completes when every queued value has been written.</summary>
    /// <exception cref="Ddc.DdcException">A write failed; values queued after it are discarded.</exception>
    public Task WriteAsync(ushort value)
    {
        lock (_lock)
        {
            _pending = value;
            return _drain ??= Task.Run(DrainAsync);
        }
    }

    private async Task DrainAsync()
    {
        try
        {
            while (TakePending() is { } value)
            {
                await channel.WriteVcpAsync(vcpCode, value, CancellationToken.None);
            }
        }
        catch
        {
            lock (_lock)
            {
                _pending = null;
                _drain = null;
            }

            throw;
        }
    }

    private ushort? TakePending()
    {
        lock (_lock)
        {
            var value = _pending;
            _pending = null;
            if (value is null)
            {
                _drain = null;
            }

            return value;
        }
    }
}
