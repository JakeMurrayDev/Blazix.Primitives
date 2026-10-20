namespace Blazix.Primitives;

/// <summary>
/// The argument of every <c>On&lt;X&gt;Change</c> callback (ADR 05 §2/§3; glossary "Details"): the requested
/// <see cref="Value"/> and the request's <see cref="Details"/>. The details are the same object for every callback in a
/// chain (EVD-EVT-04).
/// </summary>
public readonly record struct ValueChangeEventArgs<T>(T Value, ChangeEventDetails Details);
