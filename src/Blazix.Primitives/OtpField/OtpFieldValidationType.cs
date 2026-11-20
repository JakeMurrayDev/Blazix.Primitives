namespace Blazix.Primitives;

// OTP-API-07, OTP-STA-05 (otp-field/utils/otp.ts:8-37): the family-prefixed enum of §3's Enums row (ADR 08 Amendments
// 2026-11-18; issue 278, M6 track 7). `null` on the Root means numeric.
/// <summary>Specifies which characters a one-time code field accepts.</summary>
public enum OtpFieldValidationType
{
    /// <summary>The digits 0 to 9.</summary>
    Numeric,

    /// <summary>The letters A to Z and a to z.</summary>
    Alpha,

    /// <summary>The digits 0 to 9 and the letters A to Z and a to z.</summary>
    Alphanumeric,

    /// <summary>Any character; no built-in filtering.</summary>
    None,
}
