using System.Collections;

namespace Blazix.Primitives;

// FRM-API-02, FFI-API-04 (ADR 08 Amendments 2026-10-22: a FormErrors value is a string or an array of strings). A class,
// not a struct, so reading it as IReadOnlyList<string> never boxes (BLZX2005).
/// <summary>
/// Represents the error messages of one field in <see cref="FormErrors"/>: one message, several, or none. A
/// <see langword="string"/> or a <see langword="string"/> array converts to it implicitly.
/// </summary>
/// <remarks>
/// The messages count as an error when there is at least one: an empty or <see langword="null"/> string converts to no
/// messages, and an array is taken as it is, so an array holding one empty string is an error.
/// </remarks>
public sealed class FormErrorMessages : IReadOnlyList<string>
{
    private readonly string[] messages;

    private FormErrorMessages(string[] messages)
    {
        this.messages = messages;
    }

    /// <summary>Gets an instance with no messages.</summary>
    /// <value>The empty messages, which are not an error.</value>
    public static FormErrorMessages None { get; } = new([]);

    /// <inheritdoc />
    public int Count => messages.Length;

    /// <inheritdoc />
    public string this[int index] => messages[index];

    /// <summary>Converts one message.</summary>
    /// <param name="message">The message; <see langword="null"/> or empty gives <see cref="None"/>.</param>
    public static implicit operator FormErrorMessages(string? message) => string.IsNullOrEmpty(message) ? None : new([message]);

    /// <summary>Converts an array of messages, kept as it is.</summary>
    /// <param name="messages">The messages; <see langword="null"/> or empty gives <see cref="None"/>.</param>
    public static implicit operator FormErrorMessages(string[]? messages) => messages is null || messages.Length == 0 ? None : new([.. messages]);

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)messages).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => messages.GetEnumerator();
}
