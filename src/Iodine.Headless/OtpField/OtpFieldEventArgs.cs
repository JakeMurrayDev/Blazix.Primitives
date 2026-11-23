namespace Blazix.Primitives;

// OTP-API-05, OTP-EVT-07 (onValueInvalid) and OTP-API-06, OTP-EVT-08..11 (onValueComplete); ADR 08 Amendments 2026-11-18: the
// family keeps its own completion callback, not ValueCommittedEventArgs<T>; the records follow ValueChangeEventArgs<T>.
/// <summary>Represents the argument of a one-time code field's <c>OnValueInvalid</c> callback.</summary>
/// <param name="Value">The text the user entered, typed, pasted or autofilled, before any character was removed.</param>
/// <param name="Details">The details: the reason, <see cref="EventReason.InputChange"/> or <see cref="EventReason.InputPaste"/>, and the event.</param>
public readonly record struct OtpFieldInvalidEventArgs(string Value, GenericEventDetails Details);

/// <summary>Represents the argument of a one-time code field's <c>OnValueComplete</c> callback.</summary>
/// <param name="Value">The complete value.</param>
/// <param name="Details">The details: the reason, <see cref="EventReason.InputChange"/> or <see cref="EventReason.InputPaste"/>, and the event.</param>
public readonly record struct OtpFieldCompleteEventArgs(string Value, GenericEventDetails Details);
