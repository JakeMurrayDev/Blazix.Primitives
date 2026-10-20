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
    private readonly Store<TState> store;
    private readonly Action onSelectionChanged;
    private readonly List<ISelectionRefresh<TState>> selections = [];
    private IDisposable? subscription;

    /// <summary>Subscribes once to <paramref name="store"/>; <paramref name="onSelectionChanged"/> runs after a notification in which at least one selection changed.</summary>
    public SelectorSubscription(Store<TState> store, Action onSelectionChanged)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(onSelectionChanged);
        this.store = store;
        this.onSelectionChanged = onSelectionChanged;
        subscription = store.Subscribe(OnStoreChanged);
    }

    /// <summary>Adds a selection of <c>selector(state)</c>, evaluated now against the current record and again on every notification.</summary>
    public StoreSelection<TState, TValue> Select<TValue>(Func<TState, TValue> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        StoreSelection<TState, TValue> selection = new(selector, store.State);
        selections.Add(selection);
        return selection;
    }

    /// <summary>Adds a selection of <c>selector(state, arg)</c>, evaluated now against the current record and again on every notification or argument change.</summary>
    public StoreSelection<TState, TArg, TValue> Select<TArg, TValue>(Func<TState, TArg, TValue> selector, TArg arg)
    {
        ArgumentNullException.ThrowIfNull(selector);
        StoreSelection<TState, TArg, TValue> selection = new(selector, arg, store);
        selections.Add(selection);
        return selection;
    }

    /// <summary>Unsubscribes from the store; the selections keep their last values.</summary>
    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void OnStoreChanged(TState state)
    {
        // Every selection is refreshed, so each holds the value for this record even when an earlier one changed.
        bool changed = false;
        for (int i = 0; i < selections.Count; i++)
        {
            changed |= selections[i].Refresh(state);
        }

        if (changed)
        {
            onSelectionChanged();
        }
    }
}

/// <summary>One selection of a <see cref="SelectorSubscription{TState}"/>: holds <c>selector(state)</c> for the latest record, replaced only when the new result differs by <see cref="Equality.Is{T}(T, T)"/>.</summary>
public sealed class StoreSelection<TState, TValue> : ISelectionRefresh<TState>
    where TState : class, IStoreState<TState>
{
    private readonly Func<TState, TValue> selector;
    private TValue value;

    internal StoreSelection(Func<TState, TValue> selector, TState state)
    {
        this.selector = selector;
        value = selector(state);
    }

    /// <summary>The selector's result for the latest record.</summary>
    public TValue Value => value;

    bool ISelectionRefresh<TState>.Refresh(TState state)
    {
        TValue next = selector(state);
        if (Equality.Is(value, next))
        {
            return false;
        }

        value = next;
        return true;
    }
}

/// <summary>One selection with an argument of a <see cref="SelectorSubscription{TState}"/>: holds <c>selector(state, Arg)</c> for the latest record, replaced only when the new result differs by <see cref="Equality.Is{T}(T, T)"/>.</summary>
public sealed class StoreSelection<TState, TArg, TValue> : ISelectionRefresh<TState>
    where TState : class, IStoreState<TState>
{
    private readonly Func<TState, TArg, TValue> selector;
    private readonly Store<TState> store;
    private TArg arg;
    private TValue value;

    internal StoreSelection(Func<TState, TArg, TValue> selector, TArg arg, Store<TState> store)
    {
        this.selector = selector;
        this.store = store;
        this.arg = arg;
        value = selector(store.State, arg);
    }

    /// <summary>The selector's result for the latest record and <see cref="Arg"/>.</summary>
    public TValue Value => value;

    /// <summary>
    /// The argument. Setting a different one (by <see cref="Equality.Is{T}(T, T)"/>) re-evaluates <see cref="Value"/> at once
    /// without calling back, as a render with new selector arguments reads the new result (UTL-STA-18).
    /// </summary>
    public TArg Arg
    {
        get => arg;
        set
        {
            if (!Equality.Is(arg, value))
            {
                arg = value;
                this.value = selector(store.State, value);
            }
        }
    }

    bool ISelectionRefresh<TState>.Refresh(TState state)
    {
        TValue next = selector(state, arg);
        if (Equality.Is(value, next))
        {
            return false;
        }

        value = next;
        return true;
    }
}

/// <summary>Re-evaluates a selection against a new record and reports whether its value changed.</summary>
internal interface ISelectionRefresh<TState>
{
    bool Refresh(TState state);
}
