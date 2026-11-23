namespace Blazix.Primitives;

// SLD-API-26; slider/value/SliderValue.tsx:20-58 (`children(formattedValues, values)`). ADR 08 Amendments 2026-10-21 (the
// value-argument records; the Meter and Progress precedent, MeterValueArguments).
/// <summary>Represents what a slider value's template receives: the formatted values and the values.</summary>
/// <param name="FormattedValues">Each value formatted with the root's format string and culture, in thumb order.</param>
/// <param name="Values">The normalized values, in thumb order.</param>
public readonly record struct SliderValueArguments(IReadOnlyList<string> FormattedValues, IReadOnlyList<double> Values);
