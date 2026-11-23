namespace Iodine.Headless;

// ADR 08 §3's Enums row and Amendments 2026-11-18 (decision 10 (a)): a literal-valued option of NumberFormatOptions, the
// PascalCase of its Intl.NumberFormatOptions literals, beside its first consumer and named in ADR 08 §3's companion table.
/// <summary>Specifies how a number is rounded to its displayed digits.</summary>
public enum NumberFormatRoundingMode
{
    /// <summary>Toward positive infinity.</summary>
    Ceil,

    /// <summary>Toward negative infinity.</summary>
    Floor,

    /// <summary>Away from zero.</summary>
    Expand,

    /// <summary>Toward zero.</summary>
    Trunc,

    /// <summary>To the nearest value, ties toward positive infinity.</summary>
    HalfCeil,

    /// <summary>To the nearest value, ties toward negative infinity.</summary>
    HalfFloor,

    /// <summary>To the nearest value, ties away from zero.</summary>
    HalfExpand,

    /// <summary>To the nearest value, ties toward zero.</summary>
    HalfTrunc,

    /// <summary>To the nearest value, ties toward the even digit.</summary>
    HalfEven,
}
