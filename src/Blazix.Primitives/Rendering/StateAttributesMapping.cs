namespace Blazix.Primitives.Rendering;

// ADR 02 decision 4; RND §4.4. The shared mappings (transition status, the family-specific ones) are hand-written on top of this type.
/// <summary>
/// Represents a custom mapping from the fields of a state record to <c>data-*</c> attributes.
/// </summary>
/// <typeparam name="TState">The type of the part's state record whose fields are mapped.</typeparam>
/// <remarks>
/// For each mapped field, identified by property name, a <see cref="StateFieldProjection{TValue}"/> replaces the default
/// projection of that field.
/// </remarks>
public sealed class StateAttributesMapping<TState>
{
    private readonly List<KeyValuePair<string, Delegate>> entries = [];

    /// <summary>Gets the number of fields mapped.</summary>
    /// <value>The number of mapped fields.</value>
    public int Count => entries.Count;

    /// <summary>Maps <paramref name="field"/> to <paramref name="projection"/>.</summary>
    /// <typeparam name="TValue">The type of the field.</typeparam>
    /// <param name="field">The property name of the field, as given by <see langword="nameof"/>.</param>
    /// <param name="projection">The projection that replaces the default projection of the field.</param>
    /// <returns>This mapping, so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="field"/> or <paramref name="projection"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="field"/> is empty.</exception>
    /// <remarks>A later mapping of the same field replaces the earlier one.</remarks>
    public StateAttributesMapping<TState> Map<TValue>(string field, StateFieldProjection<TValue> projection)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ArgumentNullException.ThrowIfNull(projection);
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Key, field, StringComparison.Ordinal))
            {
                entries[i] = new KeyValuePair<string, Delegate>(field, projection);
                return this;
            }
        }

        entries.Add(new KeyValuePair<string, Delegate>(field, projection));
        return this;
    }

    /// <summary>Determines whether <paramref name="field"/> has a mapping.</summary>
    /// <param name="field">The property name of the field, compared ordinally.</param>
    /// <returns><see langword="true"/> if the field has a mapping; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string field)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Key, field, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // RND-DOM-07
    /// <summary>Applies the mapping of <paramref name="field"/>, if there is one, and writes its result.</summary>
    /// <typeparam name="TValue">The type of the field.</typeparam>
    /// <param name="field">The property name of the field.</param>
    /// <param name="value">The field's value.</param>
    /// <param name="writer">The sink the mapping's attributes are written to.</param>
    /// <returns>
    /// <see langword="true"/> if the field is mapped, also when the mapping returned <see langword="null"/>, which writes
    /// nothing; <see langword="false"/> if the field is unmapped, so the default projection runs.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The mapping for <paramref name="field"/> is not a <see cref="StateFieldProjection{TValue}"/> that accepts
    /// <typeparamref name="TValue"/>.
    /// </exception>
    /// <remarks>The generated projection calls this method for each field.</remarks>
    public bool TryProject<TValue>(string field, TValue value, StateAttributeWriter writer)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (!string.Equals(entries[i].Key, field, StringComparison.Ordinal))
            {
                continue;
            }

            if (entries[i].Value is not StateFieldProjection<TValue> projection)
            {
                throw new InvalidOperationException($"Blazix: the mapping for `{field}` projects a `{entries[i].Value.GetType().GenericTypeArguments[0].Name}`, but the field is a `{typeof(TValue).Name}`.");
            }

            StateDataAttribute[]? result = projection(value);
            if (result is not null)
            {
                for (int j = 0; j < result.Length; j++)
                {
                    writer.Set(result[j].Name, result[j].Value);
                }
            }

            return true;
        }

        return false;
    }
}
