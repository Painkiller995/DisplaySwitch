namespace DisplaySwitch.Core.Ddc;

/// <summary>The current and maximum values a monitor reports for one VCP feature.</summary>
public readonly record struct VcpReading(ushort Current, ushort Maximum);
