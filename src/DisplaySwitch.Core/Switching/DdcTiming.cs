namespace DisplaySwitch.Core.Switching;

/// <summary>
/// Timing policy for DDC/CI traffic. Defaults tolerate slow monitors (~60 ms per read, &gt;1 s to
/// change input) and links where roughly one request in four fails.
/// </summary>
public sealed record DdcTiming
{
    public static DdcTiming Default { get; } = new();

    /// <summary>Upper bound for one VCP read or write, including OS overhead.</summary>
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Upper bound for a capabilities request, which transfers hundreds of bytes in fragments.</summary>
    public TimeSpan CapabilitiesTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Attempts per operation before a transient failure is reported.</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>
    /// Delay before the first retry, doubled for each further retry, since errors tend to arrive in
    /// bursts. DDC/CI requires at least 50 ms between messages.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(80);

    public TimeSpan RetryDelayFor(int failedAttempts) => RetryDelay * Math.Pow(2, failedAttempts - 1);

    /// <summary>Time the monitor gets to begin switching before the first verification read.</summary>
    public TimeSpan SwitchSettleDelay { get; init; } = TimeSpan.FromMilliseconds(1200);

    /// <summary>Interval between verification reads after a switch.</summary>
    public TimeSpan VerifyPollInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>How long to keep confirming a switch before reporting an unconfirmed result.</summary>
    public TimeSpan VerifyWindow { get; init; } = TimeSpan.FromSeconds(6);
}
