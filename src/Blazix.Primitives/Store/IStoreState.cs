namespace Blazix.Primitives.Store;

// ADR 05 §1, ADR 16 rule 1
/// <summary>
/// Defines the two operations a store performs on its immutable state record: copying it and comparing it member by member.
/// </summary>
/// <typeparam name="TSelf">The state record type that implements the interface.</typeparam>
/// <remarks>
/// A generic store cannot copy or compare a record without reflection, so the record supplies both operations. Mark the
/// <c>sealed partial record</c> with <see cref="StoreStateAttribute"/> and the generator writes both:
/// <code>
/// [StoreState]
/// public sealed partial record CounterState(int Count, string Label) : IStoreState&lt;CounterState&gt;;
/// </code>
/// </remarks>
public interface IStoreState<TSelf>
    where TSelf : class, IStoreState<TSelf>
{
    // UTL-STA-12
    /// <summary>
    /// Returns a new record that holds the same member values as <paramref name="state"/>.
    /// </summary>
    /// <param name="state">The record to copy.</param>
    /// <returns>A new instance equal to <paramref name="state"/> member by member, typically <c>state with { }</c>.</returns>
    /// <remarks><see cref="Store{TState}.NotifyAll"/> uses it to give the state a new reference without changing any value.</remarks>
    static abstract TSelf Copy(TSelf state);

    // UTL-STA-11
    /// <summary>
    /// Determines whether every member of two records is equal by <see cref="Equality.Is{T}(T, T)"/>.
    /// </summary>
    /// <param name="left">The first record.</param>
    /// <param name="right">The second record.</param>
    /// <returns><see langword="true"/> if every member of <paramref name="left"/> equals the same member of <paramref name="right"/>; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <see cref="Store{TState}.Update(Func{TState, TState})"/> skips a write for which this returns <see langword="true"/>. Every
    /// member of the record must take part in the comparison.
    /// </remarks>
    static abstract bool ShallowEquals(TSelf left, TSelf right);
}
