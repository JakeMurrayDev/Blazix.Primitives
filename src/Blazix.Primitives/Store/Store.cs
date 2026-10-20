namespace Blazix.Primitives.Store;

// UTL §4.2.1; ADR 05 §1; UTL-ALG-03
/// <summary>
/// Represents an observable state container (Base UI's <c>Store</c>): one immutable record and an ordered set of listeners
/// that are notified synchronously after every write.
/// </summary>
/// <typeparam name="TState">The immutable state record.</typeparam>
/// <remarks>
/// <para>
/// Every write replaces the record, which is readable synchronously through <see cref="State"/>. Listeners run in
/// subscription order. A listener that writes during a pass starts a nested pass that notifies every listener with the
/// newer record, and the outer pass stops, so no listener receives a stale record. Equality is
/// <see cref="Equality.Is{T}(T, T)"/>.
/// </para>
/// <para>
/// A store does not marshal between threads: write to it on the renderer's synchronization context (part handlers,
/// lifecycle methods, interop callbacks), so that listeners which render run there too. A timer callback goes through
/// <c>InvokeAsync</c> first.
/// </para>
/// </remarks>
public class Store<TState>
    where TState : class, IStoreState<TState>
{
    // Insertion-ordered and iterated live (UTL §8.2): a listener removed during a pass becomes a null tombstone so the
    // pass skips it, and one added during a pass is appended and reached. Tombstones are compacted when no pass runs.
    private readonly List<Action<TState>?> listeners = [];
    private TState state;
    private int updateTick;
    private int notifyDepth;
    private bool hasTombstones;

    // UTL-STA-14
    /// <summary>
    /// Initializes a new instance of the <see cref="Store{TState}"/> class that holds <paramref name="state"/> and has no listeners.
    /// </summary>
    /// <param name="state">The initial record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public Store(TState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        this.state = state;
    }

    // UTL-STA-07
    /// <summary>
    /// Gets the current record.
    /// </summary>
    /// <value>The record written last. It is replaced on every write and is never mutated; write through the store instead.</value>
    public TState State => state;

    // UTL-STA-13
    /// <summary>
    /// Registers a listener that runs after every write with the new record.
    /// </summary>
    /// <param name="listener">The listener to call with each new record.</param>
    /// <returns>An object whose <see cref="IDisposable.Dispose"/> unregisters <paramref name="listener"/>, even during a notification pass.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="listener"/> is <see langword="null"/>.</exception>
    /// <remarks>Registering the same delegate instance twice has no further effect.</remarks>
    public IDisposable Subscribe(Action<TState> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (IndexOf(listener) < 0)
        {
            listeners.Add(listener);
        }

        return new Subscription(this, listener);
    }

    // UTL-STA-08, UTL-ALG-03
    /// <summary>
    /// Replaces the record with <paramref name="next"/> and calls every listener with it, in subscription order.
    /// </summary>
    /// <param name="next">The new record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="next"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Nothing happens when <paramref name="next"/> is the current record (the same reference). When a listener writes during
    /// the pass, that nested write notifies every listener with the newer record and the outer pass stops, so the listeners it
    /// had not reached never see the stale record.
    /// </remarks>
    public void SetState(TState next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (ReferenceEquals(state, next))
        {
            return;
        }

        state = next;
        int tick = ++updateTick;
        notifyDepth++;
        try
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                if (tick != updateTick)
                {
                    // A listener wrote again: that nested pass has already notified everyone with the newer record.
                    return;
                }

                listeners[i]?.Invoke(next);
            }
        }
        finally
        {
            if (--notifyDepth == 0 && hasTombstones)
            {
                listeners.RemoveAll(static listener => listener is null);
                hasTombstones = false;
            }
        }
    }

    // UTL-STA-11; ADR 05 §1
    /// <summary>
    /// Writes the record that <paramref name="change"/> returns for the current one, with one notification.
    /// </summary>
    /// <param name="change">A function from the current record to the new one, typically <c>state =&gt; state with { … }</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="change"/> is <see langword="null"/>.</exception>
    /// <remarks>Nothing happens when every member of the result equals the current one by <see cref="Equality.Is{T}(T, T)"/>.</remarks>
    public void Update(Func<TState, TState> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Write(change(state));
    }

    // UTL-STA-11
    /// <summary>
    /// Writes the record that <paramref name="change"/> returns for the current one and <paramref name="arg"/>, with one notification.
    /// </summary>
    /// <typeparam name="TArg">The type of the value passed to <paramref name="change"/>.</typeparam>
    /// <param name="change">A function from the current record and <paramref name="arg"/> to the new record.</param>
    /// <param name="arg">The value passed to <paramref name="change"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="change"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Nothing happens when every member of the result equals the current one by <see cref="Equality.Is{T}(T, T)"/>. Passing
    /// the values as <paramref name="arg"/> lets <paramref name="change"/> be a static lambda that allocates no closure.
    /// </remarks>
    public void Update<TArg>(Func<TState, TArg, TState> change, TArg arg)
    {
        ArgumentNullException.ThrowIfNull(change);
        Write(change(state, arg));
    }

    // UTL-STA-10
    /// <summary>
    /// Writes one member of the record.
    /// </summary>
    /// <typeparam name="TValue">The type of the member.</typeparam>
    /// <param name="field">The key of the member to write.</param>
    /// <param name="value">The new value of the member.</param>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> is <see langword="null"/>.</exception>
    /// <remarks>Nothing happens when the member already equals <paramref name="value"/> by <see cref="Equality.Is{T}(T, T)"/>.</remarks>
    public void Set<TValue>(StoreField<TState, TValue> field, TValue value)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!Equality.Is(field.Get(state), value))
        {
            SetState(field.With(state, value));
        }
    }

    // UTL-STA-12
    /// <summary>
    /// Replaces the record with a copy that holds the same values and notifies every listener.
    /// </summary>
    /// <remarks>Use it to re-broadcast when an object the record holds, such as a nested store, has changed.</remarks>
    public void NotifyAll()
    {
        SetState(TState.Copy(state));
    }

    // UTL-STA-17
    /// <summary>
    /// Evaluates a selector against the current record without subscribing.
    /// </summary>
    /// <typeparam name="TValue">The type of the selected value.</typeparam>
    /// <param name="selector">The selector, typically a <c>static readonly</c> field of the store.</param>
    /// <returns>The result of <paramref name="selector"/> for <see cref="State"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
    public TValue Select<TValue>(Func<TState, TValue> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return selector(state);
    }

    // UTL-STA-17
    /// <summary>
    /// Evaluates a selector that takes an argument against the current record without subscribing.
    /// </summary>
    /// <typeparam name="TArg">The type of the selector's argument.</typeparam>
    /// <typeparam name="TValue">The type of the selected value.</typeparam>
    /// <param name="selector">The selector, typically a <c>static readonly</c> field of the store.</param>
    /// <param name="arg">The argument passed to <paramref name="selector"/>.</param>
    /// <returns>The result of <paramref name="selector"/> for <see cref="State"/> and <paramref name="arg"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
    public TValue Select<TArg, TValue>(Func<TState, TArg, TValue> selector, TArg arg)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return selector(state, arg);
    }

    // UTL-STA-24
    /// <summary>
    /// Calls a listener with the current selection now and again whenever the selection changes.
    /// </summary>
    /// <typeparam name="TValue">The type of the selected value.</typeparam>
    /// <param name="selector">The selector to observe.</param>
    /// <param name="listener">The listener, called with the new value, the previous value and this store.</param>
    /// <returns>An object whose <see cref="IDisposable.Dispose"/> stops the observation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> or <paramref name="listener"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <paramref name="listener"/> runs once at once with <c>(current, current, store)</c>, then after every notification whose
    /// selection differs from the previous one by <see cref="Equality.Is{T}(T, T)"/>, with <c>(new, previous, store)</c>.
    /// </remarks>
    public IDisposable Observe<TValue>(Func<TState, TValue> selector, Action<TValue, TValue, Store<TState>> listener)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(listener);
        TValue previous = selector(state);
        listener(previous, previous, this);
        return Subscribe(next =>
        {
            TValue value = selector(next);
            if (!Equality.Is(previous, value))
            {
                TValue old = previous;
                previous = value;
                listener(value, old, this);
            }
        });
    }

    private void Write(TState next)
    {
        if (!ReferenceEquals(next, state) && !TState.ShallowEquals(state, next))
        {
            SetState(next);
        }
    }

    private int IndexOf(Action<TState> listener)
    {
        for (int i = 0; i < listeners.Count; i++)
        {
            if (ReferenceEquals(listeners[i], listener))
            {
                return i;
            }
        }

        return -1;
    }

    private void Unsubscribe(Action<TState> listener)
    {
        int index = IndexOf(listener);
        if (index < 0)
        {
            return;
        }

        if (notifyDepth > 0)
        {
            listeners[index] = null;
            hasTombstones = true;
        }
        else
        {
            listeners.RemoveAt(index);
        }
    }

    private sealed class Subscription(Store<TState> store, Action<TState> listener) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => store.Unsubscribe(listener);
    }
}
