namespace Blazix.Primitives.Store;

// ADR 05 §1
/// <summary>
/// Represents a typed key of one member of a state record: how to read the member and how to copy the record with the
/// member replaced.
/// </summary>
/// <typeparam name="TState">The state record.</typeparam>
/// <typeparam name="TValue">The type of the member.</typeparam>
/// <remarks>
/// <see cref="Store{TState}.Set{TValue}(StoreField{TState, TValue}, TValue)"/> and the sync methods of
/// <see cref="ComponentStore{TState}"/> take a key. Declare one per member as a <c>static readonly</c> field, so that a write
/// allocates nothing but the new record:
/// <code>public static readonly StoreField&lt;CounterState, int&gt; Count = new(nameof(Count), s =&gt; s.Count, (s, v) =&gt; s with { Count = v });</code>
/// </remarks>
public sealed class StoreField<TState, TValue>
    where TState : class
{
    private readonly Func<TState, TValue> get;
    private readonly Func<TState, TValue, TState> with;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreField{TState, TValue}"/> class.
    /// </summary>
    /// <param name="name">The member's name, used in development warnings.</param>
    /// <param name="get">A function that reads the member from a record.</param>
    /// <param name="with">A function that returns a copy of a record with the member replaced, such as <c>(s, v) =&gt; s with { X = v }</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="get"/> or <paramref name="with"/> is <see langword="null"/>.</exception>
    public StoreField(string name, Func<TState, TValue> get, Func<TState, TValue, TState> with)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(with);
        Name = name;
        this.get = get;
        this.with = with;
    }

    /// <summary>
    /// Gets the member's name.
    /// </summary>
    /// <value>The name passed to the constructor, used in development warnings.</value>
    public string Name { get; }

    /// <summary>
    /// Reads the member from a record.
    /// </summary>
    /// <param name="state">The record to read.</param>
    /// <returns>The member's value in <paramref name="state"/>.</returns>
    public TValue Get(TState state) => get(state);

    /// <summary>
    /// Returns a copy of a record with the member replaced.
    /// </summary>
    /// <param name="state">The record to copy.</param>
    /// <param name="value">The new value of the member.</param>
    /// <returns>A copy of <paramref name="state"/> whose member is <paramref name="value"/>.</returns>
    public TState With(TState state, TValue value) => with(state, value);
}
