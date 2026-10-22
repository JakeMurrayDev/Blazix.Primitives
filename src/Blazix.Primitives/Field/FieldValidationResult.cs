namespace Blazix.Primitives;

// FFI-API-01, FLD-API-04; field/root/useFieldValidation.ts:331-332 (empty results and empty entries are valid). ADR 08
// Amendments 2026-10-22: convertible from string and string[], null meaning valid; the constructor over a sequence is a
// refinement recorded by a dated ADR 08 note (a List<string> converts through it).
/// <summary>
/// Represents the result of a field's validator: the error messages that make the value invalid. A validator returns
/// <see langword="null"/>, or a result without messages, when the value is valid.
/// </summary>
/// <remarks>
/// A <see langword="string"/> or a <see langword="string"/> array converts to a result. Empty and <see langword="null"/>
/// messages are dropped, so an empty string, an empty array and an array of empty strings all mean valid.
/// </remarks>
public sealed class FieldValidationResult
{
    private static readonly string[] s_none = [];

    /// <summary>Initializes a new instance of the <see cref="FieldValidationResult"/> class.</summary>
    /// <param name="errors">The error messages; empty and <see langword="null"/> entries are dropped.</param>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public FieldValidationResult(IEnumerable<string?> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        List<string>? kept = null;
        foreach (string? error in errors)
        {
            if (!string.IsNullOrEmpty(error))
            {
                (kept ??= []).Add(error);
            }
        }

        Errors = kept is null ? s_none : kept.ToArray();
    }

    private FieldValidationResult(string error)
    {
        Errors = [error];
    }

    /// <summary>Gets the error messages.</summary>
    /// <value>The messages in the order the validator returned them; empty when the value is valid.</value>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Gets a value indicating whether the result has no error messages.</summary>
    /// <value><see langword="true"/> when <see cref="Errors"/> is empty; otherwise, <see langword="false"/>.</value>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Converts one error message to a result.</summary>
    /// <param name="error">The message, or <see langword="null"/> for a valid value.</param>
    /// <returns>A result with the message, a result without messages for an empty string, or <see langword="null"/>.</returns>
    public static implicit operator FieldValidationResult?(string? error)
    {
        if (error is null)
        {
            return null;
        }

        return error.Length == 0 ? new FieldValidationResult(s_none) : new FieldValidationResult(error);
    }

    /// <summary>Converts an array of error messages to a result.</summary>
    /// <param name="errors">The messages, or <see langword="null"/> for a valid value.</param>
    /// <returns>A result with the non-empty messages, or <see langword="null"/>.</returns>
    public static implicit operator FieldValidationResult?(string[]? errors) => errors is null ? null : new FieldValidationResult(errors);
}
