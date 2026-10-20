namespace Blazix.Primitives.Store;

/// <summary>
/// The reactive read of UTL §4.2.3 (<c>useStore</c>, <c>store.useState</c>) for one owner (ADR 05 §1, UTL-STA-25..27):
/// one store subscription however many selections the owner makes; on every notification each selection is re-evaluated
/// and <c>onSelectionChanged</c> runs once if any of them changed by <see cref="Equality.Is{T}(T, T)"/>. A part creates it
/// in <c>OnInitialized</c> with <c>StateHasChanged</c> as the callback, reads <see cref="StoreSelection{TState, TValue}.Value"/>
/// while rendering, and disposes it with the part. A notification whose selections are all unchanged allocates nothing.
/// </summary>
public sealed class SelectorSubscription<TState> : IDisposable
    where TState : class, IStoreState<TState>
{
    public SelectorSubscription(Store<TState> store, Action onSelectionChanged)
    {
        throw new NotImplementedException();
    }

    /// <summary>Adds a selection, evaluated now against the current record.</summary>
    public StoreSelection<TState, TValue> Select<TValue>(Func<TState, TValue> selector)
    {
        throw new NotImplementedException();
    }

    /// <summary>Adds a selection with an argument, evaluated now against the current record.</summary>
    public StoreSelection<TState, TArg, TValue> Select<TArg, TValue>(Func<TState, TArg, TValue> selector, TArg arg)
    {
        throw new NotImplementedException();
    }

    /// <summary>Unsubscribes from the store; the selections keep their last values.</summary>
    public void Dispose()
    {
        throw new NotImplementedException();
    }
}

/// <summary>One selection of a <see cref="SelectorSubscription{TState}"/>.</summary>
public sealed class StoreSelection<TState, TValue> : ISelectionRefresh<TState>
    where TState : class, IStoreState<TState>
{
    internal StoreSelection(Func<TState, TValue> selector, TState state)
    {
        throw new NotImplementedException();
    }

    /// <summary>The selector's result for the latest record.</summary>
    public TValue Value => throw new NotImplementedException();

    bool ISelectionRefresh<TState>.Refresh(TState state)
    {
        throw new NotImplementedException();
    }
}

/// <summary>One selection with an argument of a <see cref="SelectorSubscription{TState}"/>.</summary>
public sealed class StoreSelection<TState, TArg, TValue> : ISelectionRefresh<TState>
    where TState : class, IStoreState<TState>
{
    internal StoreSelection(Func<TState, TArg, TValue> selector, TArg arg, TState state)
    {
        throw new NotImplementedException();
    }

    /// <summary>The selector's result for the latest record and <see cref="Arg"/>.</summary>
    public TValue Value => throw new NotImplementedException();

    /// <summary>
    /// The argument. Setting a different one (by <see cref="Equality.Is{T}(T, T)"/>) re-evaluates <see cref="Value"/> at once
    /// without calling back, as a render with new selector arguments reads the new result (UTL-STA-18).
    /// </summary>
    public TArg Arg
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    bool ISelectionRefresh<TState>.Refresh(TState state)
    {
        throw new NotImplementedException();
    }
}

/// <summary>Re-evaluates a selection against a new record and reports whether its value changed.</summary>
internal interface ISelectionRefresh<TState>
{
    bool Refresh(TState state);
}
