namespace Blazix.Primitives.Store;

/// <summary>
/// A typed key of a state record (ADR 05 §1's typed <c>Set&lt;TValue&gt;</c> accessors): how to read one member and how to
/// produce a copy with that member replaced. Declare one per member as a <c>static readonly</c> field, so writes allocate
/// nothing but the new record:
/// <code>public static readonly StoreField&lt;CounterState, int&gt; Count = new(nameof(Count), s =&gt; s.Count, (s, v) =&gt; s with { Count = v });</code>
/// </summary>
public sealed class StoreField<TState, TValue>
    where TState : class
{
    private readonly Func<TState, TValue> get;
    private readonly Func<TState, TValue, TState> with;

    public StoreField(string name, Func<TState, TValue> get, Func<TState, TValue, TState> with)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(with);
        Name = name;
        this.get = get;
        this.with = with;
    }

    /// <summary>The member's name, used in development warnings.</summary>
    public string Name { get; }

    /// <summary>The member's value in <paramref name="state"/>.</summary>
    public TValue Get(TState state) => get(state);

    /// <summary>A copy of <paramref name="state"/> with the member set to <paramref name="value"/>.</summary>
    public TState With(TState state, TValue value) => with(state, value);
}
