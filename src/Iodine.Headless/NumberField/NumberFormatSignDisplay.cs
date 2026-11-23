namespace Iodine.Headless;

// ADR 08 §3's Enums row and Amendments 2026-11-18 (decision 10 (a)): a literal-valued option of NumberFormatOptions, the
// PascalCase of its Intl.NumberFormatOptions literals, beside its first consumer and named in ADR 08 §3's companion table.
/// <summary>Specifies when the sign of a number is displayed.</summary>
public enum NumberFormatSignDisplay
{
    /// <summary>The sign of negative numbers, negative zero included.</summary>
    Auto,

    /// <summary>No sign.</summary>
    Never,

    /// <summary>A sign for every number, zero included.</summary>
    Always,

    /// <summary>A sign for every number except zero.</summary>
    ExceptZero,

    /// <summary>The sign of negative numbers, negative zero excluded.</summary>
    Negative,
}
