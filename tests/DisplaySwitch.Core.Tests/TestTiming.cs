using DisplaySwitch.Core.Switching;

namespace DisplaySwitch.Core.Tests;

internal static class TestTiming
{
    /// <summary>Real-time but compressed timing so behavioral tests run in milliseconds.</summary>
    public static DdcTiming Fast { get; } = new()
    {
        CommandTimeout = TimeSpan.FromMilliseconds(500),
        CapabilitiesTimeout = TimeSpan.FromMilliseconds(500),
        MaxAttempts = 3,
        RetryDelay = TimeSpan.FromMilliseconds(1),
        SwitchSettleDelay = TimeSpan.FromMilliseconds(5),
        VerifyPollInterval = TimeSpan.FromMilliseconds(5),
        VerifyWindow = TimeSpan.FromMilliseconds(100),
    };
}
