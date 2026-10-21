namespace Blazix.Primitives;

// ADR 08 §3 (data attributes row): mirrors Base UI's separator/SeparatorDataAttributes.ts.
/// <summary>Defines the names of the data attributes that <see cref="Separator"/> renders.</summary>
public static class SeparatorDataAttributes
{
    /// <summary>Represents the attribute that holds the orientation of the separator: <c>horizontal</c> or <c>vertical</c>.</summary>
    public const string Orientation = "data-orientation";
}
