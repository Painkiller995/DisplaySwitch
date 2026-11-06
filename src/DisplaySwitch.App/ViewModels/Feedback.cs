using DisplaySwitch.Core.Inputs;
using DisplaySwitch.Core.Switching;

namespace DisplaySwitch.App.ViewModels;

public enum FeedbackKind
{
    Progress,
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>A user-facing status message.</summary>
/// <param name="Kind">Severity, which drives color and icon.</param>
/// <param name="Title">One short sentence stating what happened.</param>
/// <param name="Message">Optional explanation and next step.</param>
/// <param name="OfferRetry">Whether retrying the same action is a sensible next step.</param>
public sealed record Feedback(FeedbackKind Kind, string Title, string? Message = null, bool OfferRetry = false)
{
    /// <summary>Confirmations disappear on their own; anything needing attention stays until dismissed.</summary>
    public bool DismissesAutomatically => Kind is FeedbackKind.Success or FeedbackKind.Info;

    public bool IsProgress => Kind == FeedbackKind.Progress;

    public bool IsSuccess => Kind == FeedbackKind.Success;

    public bool IsWarning => Kind == FeedbackKind.Warning;

    public bool IsError => Kind == FeedbackKind.Error;
}

/// <summary>
/// Turns switch results into wording. It only claims what the monitor confirmed: "Now showing"
/// requires a verified read-back, and every other outcome says what is actually known.
/// </summary>
public static class SwitchFeedback
{
    public static Feedback InProgress(InputSource target) =>
        new(FeedbackKind.Progress, $"Switching to {target.DisplayName}…");

    /// <param name="result">The switch outcome.</param>
    /// <param name="monitorName">The monitor's display name.</param>
    /// <param name="hardwareId">PnP hardware ID, used for manufacturer-specific advice.</param>
    /// <param name="configuredInputs">The monitor's inputs, used to name the input it reported.</param>
    /// <returns>The feedback to show, or <see langword="null"/> when a newer request supersedes this one.</returns>
    public static Feedback? Describe(SwitchResult result, string monitorName, string hardwareId, IReadOnlyList<InputSource> configuredInputs)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(configuredInputs);

        var target = result.Target.DisplayName;
        return result.Outcome switch
        {
            SwitchOutcome.Verified => new Feedback(FeedbackKind.Success, $"Now showing {target}"),
            SwitchOutcome.AlreadyActive => new Feedback(FeedbackKind.Info, $"{target} is already on screen"),
            SwitchOutcome.SentWithoutConfirmation => new Feedback(
                FeedbackKind.Info,
                $"Switch command sent to {monitorName}",
                "The monitor stopped responding to this computer, which usually means it switched away. If it didn't, try again."),
            SwitchOutcome.NotConfirmed => new Feedback(
                FeedbackKind.Warning,
                $"{monitorName} stayed on {NameOf(result.ReportedInput, configuredInputs)}",
                $"The monitor accepted the command but didn't change input. {target} may have no active signal, or this monitor may ignore input changes over this connection.{SamsungAdvice(hardwareId)}",
                OfferRetry: true),
            SwitchOutcome.Failed => new Feedback(
                FeedbackKind.Error,
                $"Couldn't switch to {target}",
                "The monitor didn't accept the command. Make sure DDC/CI is turned on in the monitor's on-screen menu, then try again.",
                OfferRetry: true),
            SwitchOutcome.Rejected => new Feedback(
                FeedbackKind.Error,
                $"{target} isn't a configured input",
                "DisplaySwitch only sends input codes you have set up for this monitor."),
            SwitchOutcome.Superseded => null,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown switch outcome."),
        };
    }

    /// <summary>Several Samsung models ignore input changes not received over HDMI.</summary>
    private static string SamsungAdvice(string hardwareId) =>
        hardwareId.StartsWith("SAM", StringComparison.OrdinalIgnoreCase) ? " Some Samsung models only accept input changes over HDMI." : string.Empty;

    private static string NameOf(InputCode? reported, IReadOnlyList<InputSource> inputs) =>
        reported is { } code && inputs.FirstOrDefault(i => i.IsReportedAs(code)) is { } input
            ? input.DisplayName
            : "its current input";
}
