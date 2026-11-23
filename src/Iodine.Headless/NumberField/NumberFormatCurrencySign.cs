namespace Iodine.Headless;

// ADR 08 §3's Enums row and Amendments 2026-11-18 (decision 10 (a)): a literal-valued option of NumberFormatOptions, the
// PascalCase of its Intl.NumberFormatOptions literals, beside its first consumer and named in ADR 08 §3's companion table.
/// <summary>Specifies how a negative currency amount is signed.</summary>
public enum NumberFormatCurrencySign
{
    /// <summary>A minus sign, such as <c>-$1.00</c>.</summary>
    Standard,

    /// <summary>The accounting form, such as <c>($1.00)</c>.</summary>
    Accounting,
}
