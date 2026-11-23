namespace Iodine.Headless;

// ADR 08 §3's Enums row and Amendments 2026-11-18 (decision 10 (a)): a literal-valued option of NumberFormatOptions, the
// PascalCase of its Intl.NumberFormatOptions literals, beside its first consumer and named in ADR 08 §3's companion table.
/// <summary>Specifies how fraction digits and significant digits are reconciled when both are supplied.</summary>
public enum NumberFormatRoundingPriority
{
    /// <summary>The significant digits win when they are supplied.</summary>
    Auto,

    /// <summary>The rounding that keeps more digits wins.</summary>
    MorePrecision,

    /// <summary>The rounding that keeps fewer digits wins.</summary>
    LessPrecision,
}
