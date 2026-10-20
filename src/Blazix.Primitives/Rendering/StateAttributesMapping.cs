namespace Blazix.Primitives.Rendering;

/// <summary>
/// The custom attribute mapping of RND §4.4 for a state record <typeparamref name="TState"/>: per field (by property
/// name) a <see cref="StateFieldProjection{TValue}"/> that replaces the default projection of that field. The shared
/// mappings (transition status, the family-specific ones) are hand-written on top of this (ADR 02 decision 4).
/// </summary>
public sealed class StateAttributesMapping<TState>
{
    private readonly List<KeyValuePair<string, Delegate>> entries = [];

    /// <summary>The number of fields mapped.</summary>
    public int Count => entries.Count;

    /// <summary>Maps <paramref name="field"/> (the property name, <c>nameof</c>) to <paramref name="projection"/>; a later mapping of the same field replaces the earlier one.</summary>
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

    /// <summary>Whether <paramref name="field"/> has a mapping.</summary>
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

    /// <summary>
    /// Called by the generated projection for each field: applies the field's mapping, if any, writing its result and
    /// returning <c>true</c> (also when the mapping returned <c>null</c>, RND-DOM-07); returns <c>false</c> when the
    /// field is unmapped so the default projection runs.
    /// </summary>
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
                throw new InvalidOperationException($"The mapping for '{field}' projects a {entries[i].Value.GetType().GenericTypeArguments[0].Name}, but the field is a {typeof(TValue).Name}.");
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
