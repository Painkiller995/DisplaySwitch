using DisplaySwitch.Core.Inputs;

namespace DisplaySwitch.Core.Switching;

/// <summary>What is known after an input switch request. Only two outcomes count as success.</summary>
public enum SwitchOutcome
{
    /// <summary>The monitor reported the requested input after the command.</summary>
    Verified,

    /// <summary>The monitor already reported the requested input, so no command was sent.</summary>
    AlreadyActive,

    /// <summary>
    /// The command was accepted, then the monitor stopped answering. Many monitors stop answering a
    /// computer once they switch away from it, so this is likely, but not certain, to be a success.
    /// </summary>
    SentWithoutConfirmation,

    /// <summary>The command was accepted but the monitor still reports a different input.</summary>
    NotConfirmed,

    /// <summary>The monitor did not accept the command.</summary>
    Failed,

    /// <summary>The request was refused before reaching the monitor, because the code is not a configured input.</summary>
    Rejected,

    /// <summary>A newer switch request on the same monitor replaced this one.</summary>
    Superseded,
}

/// <param name="Outcome">What happened.</param>
/// <param name="Target">The input that was requested.</param>
/// <param name="ReportedInput">The input the monitor last reported, when known.</param>
/// <param name="ErrorMessage">A diagnostic message for <see cref="SwitchOutcome.Failed"/>.</param>
public sealed record SwitchResult(SwitchOutcome Outcome, InputSource Target, InputCode? ReportedInput = null, string? ErrorMessage = null)
{
    public bool IsConfirmedSuccess => Outcome is SwitchOutcome.Verified or SwitchOutcome.AlreadyActive;
}
