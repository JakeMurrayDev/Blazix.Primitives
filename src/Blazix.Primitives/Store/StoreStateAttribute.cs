namespace Blazix.Primitives.Store;

// ADR 05 §1, ADR 16 rule 1; ADR 08 §1 (beside IStoreState in the store's namespace) and §3
/// <summary>Marks a store's state record so that the generator writes its <see cref="IStoreState{TSelf}"/> members.</summary>
/// <remarks>
/// <para>
/// The record is declared as a <c>sealed partial record</c> that implements <see cref="IStoreState{TSelf}"/> with itself as
/// <c>TSelf</c> and derives from no other record. It declares neither member itself: the generated <c>Copy</c> returns
/// <c>state with { }</c>, and the generated <c>ShallowEquals</c> compares, in declaration order and stopping at the first
/// difference, every instance member that <c>with</c> copies.
/// </para>
/// <para>
/// A positional, auto-implemented, get-only or init-only property takes part through the property and a field directly,
/// whatever its accessibility. A property computed from other members holds no value of its own and a static member holds
/// none per record, so neither takes part. Each member is compared as <see cref="Equality.Is{T}(T, T)"/> compares its
/// type, with a comparison written for that type: a value type by value, with <c>NaN</c> equal to <c>NaN</c> and <c>0</c>
/// different from <c>-0</c>; a <see cref="string"/> by ordinal value; any other reference type, a record or a store
/// included, by reference.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class StoreStateAttribute : Attribute
{
}
