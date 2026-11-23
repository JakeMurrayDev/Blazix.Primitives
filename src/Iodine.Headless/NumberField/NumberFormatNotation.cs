namespace Iodine.Headless;

// ADR 08 §3's Enums row and Amendments 2026-11-18 (decision 10 (a)): a literal-valued option of NumberFormatOptions, the
// PascalCase of its Intl.NumberFormatOptions literals, beside its first consumer and named in ADR 08 §3's companion table.
/// <summary>Specifies the notation of a number.</summary>
public enum NumberFormatNotation
{
    /// <summary>Plain positional notation, such as <c>12,345</c>.</summary>
    Standard,

    /// <summary>Scientific notation, such as <c>1.235E4</c>.</summary>
    Scientific,

    /// <summary>Short compact notation, such as <c>12K</c>.</summary>
    Compact,
}
