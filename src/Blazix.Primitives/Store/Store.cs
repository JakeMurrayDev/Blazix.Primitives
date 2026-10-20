namespace Blazix.Primitives.Store;

/// <summary>
/// The observable state container of UTL §4.2.1 (Base UI's <c>Store</c>; ADR 05 §1): one immutable record, replaced on
/// every write and readable synchronously through <see cref="State"/>, and an ordered set of listeners notified
/// synchronously, in subscription order, with the UTL-ALG-03 recursion guard (a nested write runs its own full pass and
/// the outer pass stops). Equality is <see cref="Equality.Is{T}(T, T)"/>.
/// <para>
/// A store does not marshal: write to it on the renderer's synchronization context (part handlers, lifecycle methods,
/// twin callbacks), so listeners that render run there too. A C# timer callback goes through <c>InvokeAsync</c> first.
/// </para>
/// </summary>
/// <typeparam name="TState">The immutable state record.</typeparam>
public class Store<TState>
    where TState : class, IStoreState<TState>
{
    public Store(TState state)
    {
        throw new NotImplementedException();
    }

    /// <summary>The current record (UTL-STA-07). Never mutate it; write through the store.</summary>
    public TState State => throw new NotImplementedException();

    /// <summary>
    /// Registers <paramref name="listener"/> (UTL-STA-13): it runs after every write with the new record. Registering the
    /// same delegate instance twice has no further effect. Disposing the result unregisters it, even mid-notification.
    /// </summary>
    public IDisposable Subscribe(Action<TState> listener)
    {
        throw new NotImplementedException();
    }

    /// <summary>Replaces the record and notifies, unless <paramref name="next"/> is the current record (UTL-STA-08, UTL-ALG-03).</summary>
    public void SetState(TState next)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Writes <c>change(State)</c> (ADR 05 §1's <c>Update(state =&gt; state with { … })</c>) with one notification, unless
    /// every member is equal to the current one by <see cref="Equality.Is{T}(T, T)"/> (UTL-STA-11).
    /// </summary>
    public void Update(Func<TState, TState> change)
    {
        throw new NotImplementedException();
    }

    /// <summary>The closure-free form of <see cref="Update(Func{TState, TState})"/>.</summary>
    public void Update<TArg>(Func<TState, TArg, TState> change, TArg arg)
    {
        throw new NotImplementedException();
    }

    /// <summary>Writes one member (UTL-STA-10): nothing happens when it already equals <paramref name="value"/> by <see cref="Equality.Is{T}(T, T)"/>.</summary>
    public void Set<TValue>(StoreField<TState, TValue> field, TValue value)
    {
        throw new NotImplementedException();
    }

    /// <summary>Replaces the record with a copy holding the same values and notifies (UTL-STA-12), e.g. when a nested store changed.</summary>
    public void NotifyAll()
    {
        throw new NotImplementedException();
    }

    /// <summary>Evaluates a named selector against the current record without subscribing (UTL-STA-17).</summary>
    public TValue Select<TValue>(Func<TState, TValue> selector)
    {
        throw new NotImplementedException();
    }

    /// <summary>Evaluates a named selector with an argument against the current record without subscribing (UTL-STA-17).</summary>
    public TValue Select<TArg, TValue>(Func<TState, TArg, TValue> selector, TArg arg)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Observes a selection (UTL-STA-24): calls <paramref name="listener"/> at once with <c>(current, current, store)</c>,
    /// then after every notification whose selection differs by <see cref="Equality.Is{T}(T, T)"/> with
    /// <c>(new, previous, store)</c>. Disposing the result stops it.
    /// </summary>
    public IDisposable Observe<TValue>(Func<TState, TValue> selector, Action<TValue, TValue, Store<TState>> listener)
    {
        throw new NotImplementedException();
    }
}
