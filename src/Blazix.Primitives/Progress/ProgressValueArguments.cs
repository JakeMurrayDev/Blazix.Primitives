namespace Blazix.Primitives;

// PRG-API-10: Base UI's `children` function receives (formattedValue | 'indeterminate', value). The port's template
// parameter ProgressValue.ValueContent receives this record (ADR 08 §3: the argument record's name is the port's, as
// MeterValueArguments). The two sentinels differ by design: getAriaValueText receives '' for the same status (§16 item 3).
/// <summary>Represents what a <see cref="ProgressValue"/> template receives to render.</summary>
/// <param name="FormattedValue">The text the part shows by default while the progress is determinate: the value limited to the range and formatted, or its position within the range as a percent. While it is indeterminate, the text <c>indeterminate</c>.</param>
/// <param name="Value">The raw value the root received, not limited to the range, including <see langword="null"/>, <see cref="double.NaN"/> and the infinities.</param>
public readonly record struct ProgressValueArguments(string FormattedValue, double? Value);
