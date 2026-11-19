namespace Blazix.Primitives;

// OTP-API-09 (the `inputMode` prop's literals); ADR 08 Amendments 2026-11-18: eight members, the PascalCase of the
// `inputmode` literals; `None` is a literal, not a sentinel, and `null` on the Root means not supplied.
/// <summary>Specifies the virtual keyboard a text input asks for, the HTML <c>inputmode</c> attribute.</summary>
public enum InputMode
{
    /// <summary>No virtual keyboard: <c>none</c>.</summary>
    None,

    /// <summary>The standard text keyboard: <c>text</c>.</summary>
    Text,

    /// <summary>A telephone keypad: <c>tel</c>.</summary>
    Tel,

    /// <summary>A keyboard for URLs: <c>url</c>.</summary>
    Url,

    /// <summary>A keyboard for email addresses: <c>email</c>.</summary>
    Email,

    /// <summary>A numeric keypad: <c>numeric</c>.</summary>
    Numeric,

    /// <summary>A numeric keypad with a decimal separator: <c>decimal</c>.</summary>
    Decimal,

    /// <summary>A keyboard for search queries: <c>search</c>.</summary>
    Search,
}
