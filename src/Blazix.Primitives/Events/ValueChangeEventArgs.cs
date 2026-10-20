namespace Blazix.Primitives;

// EVD-EVT-04; ADR 05 §2/§3; glossary "Details"
/// <summary>
/// The argument of every <c>On&lt;X&gt;Change</c> callback: the requested
/// <see cref="Value"/> and the request's <see cref="Details"/>. The details are the same object for every callback in a
/// chain.
/// </summary>
public readonly record struct ValueChangeEventArgs<T>(T Value, ChangeEventDetails Details);
