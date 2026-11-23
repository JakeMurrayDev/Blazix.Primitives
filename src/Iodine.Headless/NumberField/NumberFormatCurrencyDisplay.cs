namespace Iodine.Headless;

// ADR 08 §3's Enums row and Amendments 2026-11-18 (decision 10 (a)): a literal-valued option of NumberFormatOptions, the
// PascalCase of its Intl.NumberFormatOptions literals, beside its first consumer and named in ADR 08 §3's companion table.
/// <summary>Specifies how a currency is displayed.</summary>
public enum NumberFormatCurrencyDisplay
{
    /// <summary>The currency's symbol in the culture, such as <c>€</c>.</summary>
    Symbol,

    /// <summary>The currency's narrow symbol, such as <c>$</c> for every dollar currency.</summary>
    NarrowSymbol,

    /// <summary>The ISO 4217 code, such as <c>EUR</c>.</summary>
    Code,

    /// <summary>The currency's name, such as <c>euros</c>.</summary>
    Name,
}
