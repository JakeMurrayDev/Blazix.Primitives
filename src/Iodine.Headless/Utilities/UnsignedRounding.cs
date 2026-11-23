namespace Iodine.Headless.Utilities;

// ECMA-402 GetUnsignedRoundingMode's five results (NUM-STA-12; ADR 01 Amendments 2026-10-20).
/// <summary>Specifies the rounding of a number's magnitude (ECMA-402's unsigned rounding modes).</summary>
internal enum UnsignedRounding
{
    /// <summary>Toward zero.</summary>
    Zero,

    /// <summary>Away from zero.</summary>
    Infinity,

    /// <summary>To the nearest, ties toward zero.</summary>
    HalfZero,

    /// <summary>To the nearest, ties away from zero.</summary>
    HalfInfinity,

    /// <summary>To the nearest, ties toward the even digit.</summary>
    HalfEven,
}
