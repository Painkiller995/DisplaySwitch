namespace DisplaySwitch.Core.Adjustments;

/// <summary>A continuous VCP feature and its range, as last reported by the monitor.</summary>
/// <param name="Code">VCP feature code.</param>
/// <param name="Value">Current value.</param>
/// <param name="Maximum">Largest accepted value; the minimum is always 0.</param>
public sealed record AdjustableFeature(byte Code, ushort Value, ushort Maximum)
{
    public ushort Clamp(int value) => (ushort)Math.Clamp(value, 0, Maximum);
}

/// <summary>The result of changing an adjustable feature.</summary>
public enum AdjustmentOutcome
{
    /// <summary>The monitor reports the requested value.</summary>
    Applied,

    /// <summary>The monitor accepted the command but reports a different value.</summary>
    NotApplied,

    /// <summary>The command failed or the monitor stopped answering.</summary>
    Failed,

    /// <summary>A newer value for the same feature replaced this one.</summary>
    Superseded,
}

/// <param name="Outcome">What happened.</param>
/// <param name="Feature">The feature as the monitor now reports it, or the last known state on failure.</param>
public sealed record AdjustmentResult(AdjustmentOutcome Outcome, AdjustableFeature Feature);
