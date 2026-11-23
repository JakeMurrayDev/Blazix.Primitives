namespace Blazix.Primitives.Store;

// ADR 05 §1, UTL-STA-25..27; UTL §4.2.3
/// <summary>
/// Represents the reactive read of a store for one owner (Base UI's <c>useStore</c> and <c>store.useState</c>): one store
/// subscription for any number of selections, with one callback per notification that changed at least one of them.
/// </summary>
/// <typeparam name="TState">The type of the immutable state record.</typeparam>
/// <remarks>
/// On every notification each selection is re-evaluated, and the callback runs once if any of them changed by
/// <see cref="Equality.Is{T}(T, T)"/>. A part normally gets one through
/// <see cref="Rendering.PartBase{TState}.SubscribeTo{TStoreState}(Store{TStoreState})"/>, which passes
/// <see cref="Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged"/> as the callback; it reads <see cref="StoreSelection{TState, TValue}.Value"/> while rendering, and the
/// subscription is disposed with the part. A notification whose selections are all unchanged allocates nothing.
/// </remarks>
public sealed class SelectorSubscription<TState> : IDisposable
    where TState : class, IStoreState<TState>
{
    private readonly Store<TState> store;
    private readonly Action onSelectionChanged;
    private readonly List<ISelectionRefresh<TState>> selections = [];
    private IDisposable? subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectorSubscription{TState}"/> class and subscribes once to <paramref name="store"/>.
    /// </summary>
    /// <param name="store">The store to read.</param>
    /// <param name="onSelectionChanged">The callback that runs after a notification in which at least one selection changed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> or <paramref name="onSelectionChanged"/> is <see langword="null"/>.</exception>
    public SelectorSubscription(Store<TState> store, Action onSelectionChanged)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(onSelectionChanged);
        this.store = store;
        this.onSelectionChanged = onSelectionChanged;
        subscription = store.Subscribe(OnStoreChanged);
    }

    /// <inheritdoc />
    /// <remarks>Unsubscribes from the store; the selections keep their last values. Calling it again does nothing.</remarks>
    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    /// <summary>
    /// Adds a selection that holds the result of <paramref name="selector"/> for the latest record.
    /// </summary>
    /// <typeparam name="TValue">The type of the selected value.</typeparam>
    /// <param name="selector">The selector, evaluated now against the current record and again on every notification.</param>
    /// <returns>The new selection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
    public StoreSelection<TState, TValue> Select<TValue>(Func<TState, TValue> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        StoreSelection<TState, TValue> selection = new(selector, store.State);
        selections.Add(selection);
        return selection;
    }

    /// <summary>
    /// Adds a selection that holds the result of <paramref name="selector"/> for the latest record and an argument.
    /// </summary>
    /// <typeparam name="TArg">The type of the selector's argument.</typeparam>
    /// <typeparam name="TValue">The type of the selected value.</typeparam>
    /// <param name="selector">The selector, evaluated now against the current record and again on every notification or argument change.</param>
    /// <param name="arg">The initial argument.</param>
    /// <returns>The new selection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
    public StoreSelection<TState, TArg, TValue> Select<TArg, TValue>(Func<TState, TArg, TValue> selector, TArg arg)
    {
        ArgumentNullException.ThrowIfNull(selector);
        StoreSelection<TState, TArg, TValue> selection = new(selector, arg, store);
        selections.Add(selection);
        return selection;
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

/// <summary>
/// Represents one selection of a <see cref="SelectorSubscription{TState}"/>.
/// </summary>
/// <typeparam name="TState">The type of the immutable state record.</typeparam>
/// <typeparam name="TValue">The type of the selected value.</typeparam>
/// <remarks>The held value is replaced only when the selector's new result differs from it by <see cref="Equality.Is{T}(T, T)"/>.</remarks>
public sealed class StoreSelection<TState, TValue> : ISelectionRefresh<TState>
    where TState : class, IStoreState<TState>
{
    private readonly Func<TState, TValue> selector;
    private TValue value;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreSelection{TState, TValue}"/> class and evaluates it against <paramref name="state"/>.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <param name="state">The current record.</param>
    internal StoreSelection(Func<TState, TValue> selector, TState state)
    {
        this.selector = selector;
        value = selector(state);
    }

    /// <summary>
    /// Gets the selector's result for the latest record.
    /// </summary>
    /// <value>The value selected from the record of the latest notification, or from the current record at creation.</value>
    public TValue Value => value;

    /// <inheritdoc />
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

/// <summary>
/// Represents one selection that takes an argument, in a <see cref="SelectorSubscription{TState}"/>.
/// </summary>
/// <typeparam name="TState">The type of the immutable state record.</typeparam>
/// <typeparam name="TArg">The type of the selector's argument.</typeparam>
/// <typeparam name="TValue">The type of the selected value.</typeparam>
/// <remarks>The held value is replaced only when the selector's new result differs from it by <see cref="Equality.Is{T}(T, T)"/>.</remarks>
public sealed class StoreSelection<TState, TArg, TValue> : ISelectionRefresh<TState>
    where TState : class, IStoreState<TState>
{
    private readonly Func<TState, TArg, TValue> selector;
    private readonly Store<TState> store;
    private TArg arg;
    private TValue value;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreSelection{TState, TArg, TValue}"/> class and evaluates it against the
    /// current record of <paramref name="store"/>.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <param name="arg">The initial argument.</param>
    /// <param name="store">The store whose current record the selection reads when the argument changes.</param>
    internal StoreSelection(Func<TState, TArg, TValue> selector, TArg arg, Store<TState> store)
    {
        this.selector = selector;
        this.store = store;
        this.arg = arg;
        value = selector(store.State, arg);
    }

    /// <summary>
    /// Gets the selector's result for the latest record and <see cref="Arg"/>.
    /// </summary>
    /// <value>The value selected from the record of the latest notification, or from the current record at creation or at the last argument change.</value>
    public TValue Value => value;

    // UTL-STA-18
    /// <summary>
    /// Gets or sets the argument passed to the selector.
    /// </summary>
    /// <value>The current argument.</value>
    /// <remarks>
    /// Setting an argument that differs by <see cref="Equality.Is{T}(T, T)"/> re-evaluates <see cref="Value"/> at once without
    /// calling back, as a render with new selector arguments reads the new result.
    /// </remarks>
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

    /// <inheritdoc />
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

/// <summary>
/// Defines how a selection is re-evaluated against a new record.
/// </summary>
/// <typeparam name="TState">The type of the immutable state record.</typeparam>
internal interface ISelectionRefresh<TState>
{
    /// <summary>
    /// Evaluates the selection against <paramref name="state"/> and keeps the result when it differs from the held value.
    /// </summary>
    /// <param name="state">The new record.</param>
    /// <returns><see langword="true"/> if the result differed by <see cref="Equality.Is{T}(T, T)"/> and was kept; otherwise, <see langword="false"/>.</returns>
    bool Refresh(TState state);
}
