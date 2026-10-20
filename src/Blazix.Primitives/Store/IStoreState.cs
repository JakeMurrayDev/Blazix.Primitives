namespace Blazix.Primitives.Store;

/// <summary>
/// The two operations a store needs on its immutable state record and cannot write generically without reflection
/// (ADR 05 §1, ADR 16 rule 1). Implement both on the <c>sealed record</c>:
/// <code>
/// public static CounterState Copy(CounterState state) =&gt; state with { };
/// public static bool ShallowEquals(CounterState left, CounterState right) =&gt;
///     Equality.Is(left.Count, right.Count) &amp;&amp; Equality.Is(left.Label, right.Label);
/// </code>
/// </summary>
/// <typeparam name="TSelf">The state record itself.</typeparam>
public interface IStoreState<TSelf>
    where TSelf : class, IStoreState<TSelf>
{
    /// <summary>A new record holding the same member values (<c>state with { }</c>), for <see cref="Store{TState}.NotifyAll"/> (UTL-STA-12).</summary>
    static abstract TSelf Copy(TSelf state);

    /// <summary>
    /// Whether every member of <paramref name="left"/> and <paramref name="right"/> is equal by <see cref="Equality.Is{T}(T, T)"/>:
    /// the no-op test of <see cref="Store{TState}.Update(Func{TState, TState})"/> (UTL-STA-11). Every member must take part.
    /// </summary>
    static abstract bool ShallowEquals(TSelf left, TSelf right);
}
