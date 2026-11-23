namespace Blazix.Primitives.Utilities;

// UTL-I18N-05, NUM-I18N-07: one element of Intl.NumberFormat.prototype.formatToParts's result.
/// <summary>Represents a piece of formatted number text and its kind.</summary>
/// <param name="Type">The kind of the piece.</param>
/// <param name="Value">The text of the piece.</param>
internal readonly record struct NumberFormatPart(NumberFormatPartType Type, string Value);
