using DisplaySwitch.Core.Inputs;

namespace DisplaySwitch.Core.Ddc;

/// <summary>The parsed form of a monitor's MCCS capabilities string.</summary>
/// <param name="Model">The <c>model(...)</c> entry, often an internal codename such as "FALCON".</param>
/// <param name="MccsVersion">The <c>mccs_ver(...)</c> entry, such as "2.1".</param>
/// <param name="VcpFeatures">Advertised VCP codes, each with its advertised discrete values (if any).</param>
public sealed record MonitorCapabilities(
    string? Model,
    string? MccsVersion,
    IReadOnlyDictionary<byte, IReadOnlyList<byte>> VcpFeatures)
{
    public static MonitorCapabilities Empty { get; } = new(null, null, new Dictionary<byte, IReadOnlyList<byte>>());

    public bool SupportsInputSelect => VcpFeatures.ContainsKey(VcpCodes.InputSelect);

    /// <summary>The input codes the monitor advertises for VCP 0x60, excluding the reserved value 0.</summary>
    public IReadOnlyList<InputCode> AdvertisedInputs =>
        VcpFeatures.TryGetValue(VcpCodes.InputSelect, out var values)
            ? [.. values.Where(v => v != 0).Distinct().Select(v => new InputCode(v))]
            : [];
}
