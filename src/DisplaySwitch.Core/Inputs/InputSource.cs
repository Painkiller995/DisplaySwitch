using System.Text.Json.Serialization;

namespace DisplaySwitch.Core.Inputs;

/// <summary>One selectable input on a monitor, including the user's personalization.</summary>
public sealed record InputSource
{
    public const int MaxLabelLength = 40;

    /// <summary>The value written to VCP 0x60 to select this input.</summary>
    public required InputCode Code { get; init; }

    /// <summary>The connector name printed on the monitor, such as "HDMI 1".</summary>
    public required string PortName { get; init; }

    public InputKind Kind { get; init; }

    /// <summary>The user's name for the device on this input, such as "Work laptop".</summary>
    public string? Label { get; init; }

    public InputIcon Icon { get; init; }

    public bool IsHidden { get; init; }

    /// <summary>Other values the monitor may report while this input is active (some read back a different value than written).</summary>
    public IReadOnlyList<InputCode> ReadAliases { get; init; } = [];

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Label) ? PortName : Label;

    public bool IsReportedAs(InputCode reported) => reported == Code || ReadAliases.Contains(reported);
}
