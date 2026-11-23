using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Blazix.Primitives;

// FRM-API-02, FFI-API-04 (ADR 08 Amendments 2026-10-22). Keys keep their insertion order (a JavaScript object's own string
// keys enumerate in insertion order). FFI-API-04's inherited-property and prototype-less clauses have no C# meaning (port
// note on FRM-API-02). Form compares the map by reference: a new instance replaces Form's current errors, and the keys
// fields clear stay cleared until then (FRM-STA-02, FFI-EVT-04).
/// <summary>
/// Represents the errors a form shows for its fields, by field name, usually returned by a server after a submission.
/// </summary>
/// <remarks>
/// <para>
/// A value is a <see langword="string"/> or a <see langword="string"/> array:
/// <c>new FormErrors { ["url"] = "Not a valid URL", ["tags"] = new[] { "Too many", "Duplicate" } }</c>. A key is an error
/// for the field with that name only when it has at least one message.
/// </para>
/// <para>
/// Give the form a new instance to replace its errors. The form does not watch an instance for changes, and it never
/// modifies one: a field whose value changes stops showing its form error until the form receives a new instance.
/// </para>
/// </remarks>
public sealed class FormErrors : IReadOnlyDictionary<string, IReadOnlyList<string>>
{
    private readonly List<string> order = [];
    private readonly Dictionary<string, FormErrorMessages> entries = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public int Count => order.Count;

    /// <inheritdoc />
    /// <remarks>The field names are in the order they were added.</remarks>
    public IEnumerable<string> Keys => order;

    /// <summary>Gets the messages of every field, in the order the fields were added.</summary>
    /// <value>The values.</value>
    public IEnumerable<FormErrorMessages> Values
    {
        get
        {
            foreach (string key in order)
            {
                yield return entries[key];
            }
        }
    }

    /// <inheritdoc />
    IEnumerable<IReadOnlyList<string>> IReadOnlyDictionary<string, IReadOnlyList<string>>.Values => Values;

    /// <summary>Gets or sets the messages of a field.</summary>
    /// <param name="key">The field's name.</param>
    /// <value>The messages; a <see langword="string"/> or a <see langword="string"/> array converts implicitly.</value>
    /// <exception cref="KeyNotFoundException">The getter is called with a name that has no entry.</exception>
    public FormErrorMessages this[string key]
    {
        get => entries[key];
        set
        {
            ArgumentNullException.ThrowIfNull(key);
            if (!entries.ContainsKey(key))
            {
                order.Add(key);
            }

            entries[key] = value ?? FormErrorMessages.None;
        }
    }

    /// <inheritdoc />
    IReadOnlyList<string> IReadOnlyDictionary<string, IReadOnlyList<string>>.this[string key] => this[key];

    /// <inheritdoc />
    public bool ContainsKey(string key) => entries.ContainsKey(key);

    /// <summary>Determines whether a field has an error: an entry with at least one message.</summary>
    /// <param name="name">The field's name.</param>
    /// <returns><see langword="true"/> when the entry exists and has at least one message; otherwise, <see langword="false"/>.</returns>
    public bool HasError(string name) => entries.TryGetValue(name, out FormErrorMessages? messages) && messages.Count > 0;

    /// <summary>Gets the messages of a field.</summary>
    /// <param name="key">The field's name.</param>
    /// <param name="value">The messages when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the field has an entry; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out FormErrorMessages value) => entries.TryGetValue(key, out value);

    /// <inheritdoc />
    bool IReadOnlyDictionary<string, IReadOnlyList<string>>.TryGetValue(string key, [MaybeNullWhen(false)] out IReadOnlyList<string> value)
    {
        bool found = entries.TryGetValue(key, out FormErrorMessages? messages);
        value = messages;
        return found;
    }

    /// <summary>Removes the entry of a field.</summary>
    /// <param name="key">The field's name.</param>
    /// <returns><see langword="true"/> when an entry was removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(string key)
    {
        if (!entries.Remove(key))
        {
            return false;
        }

        order.Remove(key);
        return true;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, IReadOnlyList<string>>> GetEnumerator()
    {
        foreach (string key in order)
        {
            yield return new KeyValuePair<string, IReadOnlyList<string>>(key, entries[key]);
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
