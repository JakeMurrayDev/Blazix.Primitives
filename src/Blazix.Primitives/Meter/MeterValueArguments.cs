namespace Blazix.Primitives;

// MTR-API-10: Base UI's `children` function receives (formattedValue, value). The port's template parameter
// MeterValue.ValueContent receives this record (ADR 08 §3: the argument record's name is the port's; the brief for issue 59
// leaves it to the track).
/// <summary>Represents what a <see cref="MeterValue"/> template receives to render.</summary>
/// <param name="FormattedValue">The text the part shows by default: the value limited to the range and formatted, or its position within the range as a percent.</param>
/// <param name="Value">The raw value the root received, not limited to the range.</param>
public readonly record struct MeterValueArguments(string FormattedValue, double Value);
