using DisplaySwitch.Core.Inputs;

namespace DisplaySwitch.Core.Profiles;

/// <summary>Known-good input definitions for a monitor model whose capabilities string cannot be trusted.</summary>
/// <param name="HardwareId">PnP hardware ID, such as "SAM0F21".</param>
/// <param name="ModelName">Retail model name shown to users.</param>
/// <param name="Inputs">The model's inputs, in the order printed on the monitor.</param>
public sealed record MonitorProfile(string HardwareId, string ModelName, IReadOnlyList<InputSource> Inputs);
