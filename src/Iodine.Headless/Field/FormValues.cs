using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Blazix.Primitives;

// FFI-API-07, FFI-EVT-05, FRM-EVT-04; form/Form.tsx:128-135 and field/root/useFieldValidation.ts:293-298 (the values
// object). ADR 08 Amendments 2026-10-22: read-only, typed accessors over the one object? collection; the accessor set is
// a refinement recorded by a dated ADR 08 note. ADR 05 Amendments 2026-10-22: the entries box once per validation run or
// submission, never per render. Order: a JavaScript object literal's, so a name keeps the position of its first entry and
// the value of its last.
/// <summary>
/// Represents the values of a form's named fields, by name, in the order the fields registered: what a field's validator
/// and a form's submit callbacks receive.
/// </summary>
/// <remarks>
/// Each value is the control's own value: a <see langword="string"/> for <see cref="FieldControl"/> and
/// <see cref="Input"/>, a <see langword="bool"/> for a switch or a checkbox, and so on. A field without a name is absent.
/// When two fields share a name, the name keeps the position of the first and the value of the last.
/// </remarks>
public sealed class FormValues : IReadOnlyDictionary<string, object?>
{
    private readonly string[] names;
    private readonly object?[] values;

    private FormValues(string[] names, object?[] values)
    {
        this.names = names;
        this.values = values;
    }

    /// <summary>Gets the values of a form with no named field.</summary>
    /// <value>An empty instance.</value>
    public static FormValues Empty { get; } = new([], []);

    /// <inheritdoc />
    /// <remarks>The count is the number of distinct names.</remarks>
    public int Count => names.Length;

    /// <inheritdoc />
    /// <remarks>The names come in the order the fields registered.</remarks>
    public IEnumerable<string> Keys => names;

    /// <inheritdoc />
    /// <remarks>The values come in the order of <see cref="Keys"/>.</remarks>
    public IEnumerable<object?> Values => values;

    /// <inheritdoc />
    /// <remarks>Names are compared ordinally.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">No field has that name.</exception>
    public object? this[string key] => TryGetValue(key, out object? value) ? value : throw new KeyNotFoundException($"Blazix: no field is named `{key}`.");

    /// <inheritdoc />
    /// <remarks>Names are compared ordinally.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public bool ContainsKey(string key) => IndexOf(key) >= 0;

    /// <inheritdoc />
    /// <remarks>Names are compared ordinally.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value)
    {
        int index = IndexOf(key);
        if (index < 0)
        {
            value = null;
            return false;
        }

        value = values[index];
        return true;
    }

    /// <summary>Gets the value of the field with the given name as <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The type of the value, such as <see langword="string"/> or <see langword="bool"/>.</typeparam>
    /// <param name="key">The field name, compared ordinally.</param>
    /// <param name="value">The value, or the default of <typeparamref name="T"/> when the method returns <see langword="false"/>.</param>
    /// <returns>
    /// <see langword="true"/> if a field has that name and its value is a <typeparamref name="T"/>, or is
    /// <see langword="null"/> and <typeparamref name="T"/> accepts <see langword="null"/>; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
    {
        int index = IndexOf(key);
        if (index >= 0)
        {
            object? stored = values[index];
            if (stored is T typed)
            {
                value = typed;
                return true;
            }

            if (stored is null && default(T) is null)
            {
                value = default!;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        for (int i = 0; i < names.Length; i++)
        {
            yield return new KeyValuePair<string, object?>(names[i], values[i]);
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // FFI-API-07: named entries only; a repeated name keeps its first position and takes its last value.
    /// <summary>Creates the values of the given entries, in order.</summary>
    /// <param name="entries">The name and value of each registered field, in registration order; unnamed ones are skipped.</param>
    /// <returns>The values, or <see cref="Empty"/> when no entry has a name.</returns>
    internal static FormValues Create(IEnumerable<KeyValuePair<string?, object?>> entries)
    {
        List<string>? names = null;
        List<object?>? values = null;
        foreach (KeyValuePair<string?, object?> entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Key))
            {
                continue;
            }

            names ??= [];
            values ??= [];
            int existing = names.IndexOf(entry.Key);
            if (existing >= 0)
            {
                values[existing] = entry.Value;
            }
            else
            {
                names.Add(entry.Key);
                values.Add(entry.Value);
            }
        }

        return names is null ? Empty : new FormValues([.. names], [.. values!]);
    }

    private int IndexOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
