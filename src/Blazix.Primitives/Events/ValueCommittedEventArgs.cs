namespace Blazix.Primitives;

// Red stub (M6 track 1): the shape is present, its values are wrong; the next commit replaces it with the record.
/// <summary>Represents the argument of every <c>On&lt;X&gt;Committed</c> callback.</summary>
/// <typeparam name="T">The type of the value.</typeparam>
public readonly struct ValueCommittedEventArgs<T>
{
    /// <summary>Initializes a new instance of the <see cref="ValueCommittedEventArgs{T}"/> struct.</summary>
    /// <param name="value">The committed value.</param>
    /// <param name="details">The notification's details.</param>
    public ValueCommittedEventArgs(T value, GenericEventDetails details)
    {
        _ = value;
        _ = details;
    }

    /// <summary>Gets the committed value.</summary>
    /// <value>The committed value.</value>
    public T Value => default!;

    /// <summary>Gets the notification's details.</summary>
    /// <value>The notification's details.</value>
    public GenericEventDetails Details => null!;
}
