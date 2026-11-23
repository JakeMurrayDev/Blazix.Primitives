namespace Blazix.Primitives;

// EVD-EVT-04; ADR 05 §2/§3; glossary "Details"
/// <summary>
/// Represents the argument of every <c>On&lt;X&gt;Change</c> callback: the requested value and the request's details.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <param name="Value">The requested value.</param>
/// <param name="Details">The request's details, the same object for every callback in a chain.</param>
public readonly record struct ValueChangeEventArgs<T>(T Value, ChangeEventDetails Details);
