namespace Blazix.Primitives;

// ADR 08 §3's Enums row and Amendments 2026-11-18 (decision 10 (a)): a literal-valued option of NumberFormatOptions, the
// PascalCase of its Intl.NumberFormatOptions literals, beside its first consumer and named in ADR 08 §3's companion table.
/// <summary>Specifies the formatting style of a number.</summary>
public enum NumberFormatStyle
{
    /// <summary>A plain number, such as <c>1,234.5</c>.</summary>
    Decimal,

    /// <summary>A percentage: the value is multiplied by 100 and shown with a percent sign, so 0.25 reads <c>25%</c>.</summary>
    Percent,

    /// <summary>An amount of the currency that <see cref="NumberFormatOptions.Currency"/> names, such as <c>$1,234.50</c>.</summary>
    Currency,

    /// <summary>A measure in the unit that <see cref="NumberFormatOptions.Unit"/> names, such as <c>12 km/h</c>.</summary>
    Unit,
}
