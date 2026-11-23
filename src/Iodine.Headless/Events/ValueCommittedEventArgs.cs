namespace Blazix.Primitives;

// The M6 plan's decision 13 (a); ADR 08 Amendments 2026-11-18, the commit-callback line, with `Details` confirmed as
// GenericEventDetails (ADR 05 §3, EVD-API-02; SLD-API-04, NUM-API-04: a notification, not cancelable); the twin of
// ValueChangeEventArgs<T>.
/// <summary>
/// Represents the argument of every <c>On&lt;X&gt;Committed</c> callback: the committed value and the notification's
/// details.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <param name="Value">The committed value.</param>
/// <param name="Details">The notification's details: its reason and the native event that caused it.</param>
public readonly record struct ValueCommittedEventArgs<T>(T Value, GenericEventDetails Details);
