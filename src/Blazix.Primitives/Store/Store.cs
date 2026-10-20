namespace Blazix.Primitives.Store;

// UTL §4.2.1; ADR 05 §1; UTL-ALG-03
/// <summary>
/// An observable state container (Base UI's <c>Store</c>): one immutable record, replaced on every write and readable
/// synchronously through <see cref="State"/>, and an ordered set of listeners notified synchronously, in subscription
/// order. A listener that writes during a pass starts a nested pass that notifies everyone with the newer record, and the
/// outer pass stops. Equality is <see cref="Equality.Is{T}(T, T)"/>.
/// <para>
/// A store does not marshal: write to it on the renderer's synchronization context (part handlers, lifecycle methods,
/// twin callbacks), so listeners that render run there too. A C# timer callback goes through <c>InvokeAsync</c> first.
/// </para>
/// </summary>
/// <typeparam name="TState">The immutable state record.</typeparam>
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
    /// <summary>Creates a store holding <paramref name="state"/> (not <c>null</c>) and no listeners. Each instance is independent.</summary>
    public Store(TState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        this.state = state;
    }

    // UTL-STA-07
    /// <summary>The current record. Never mutate it; write through the store.</summary>
    public TState State => state;

    // UTL-STA-13
    /// <summary>
    /// Registers <paramref name="listener"/>: it runs after every write with the new record. Registering the
    /// same delegate instance twice has no further effect. Disposing the result unregisters it, even mid-notification.
    /// </summary>
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
    /// Replaces the record with <paramref name="next"/> and calls every listener with it, in subscription order; does
    /// nothing when <paramref name="next"/> is the current record (same reference). When a listener writes during the
    /// pass, that nested write notifies every listener with the newer record and the outer pass stops, so the listeners
    /// it had not reached never see the stale record.
    /// </summary>
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
    /// Writes <c>change(State)</c>, typically <c>state =&gt; state with { … }</c>, with one notification, unless
    /// every member is equal to the current one by <see cref="Equality.Is{T}(T, T)"/>.
    /// </summary>
    public void Update(Func<TState, TState> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Write(change(state));
    }

    // UTL-STA-11
    /// <summary>
    /// Writes <c>change(State, arg)</c> with one notification, unless every member of the result is equal to the current
    /// one by <see cref="Equality.Is{T}(T, T)"/>. Passing the values as <paramref name="arg"/> lets <paramref name="change"/>
    /// be a static lambda that allocates no closure.
    /// </summary>
    public void Update<TArg>(Func<TState, TArg, TState> change, TArg arg)
    {
        ArgumentNullException.ThrowIfNull(change);
        Write(change(state, arg));
    }

    // UTL-STA-10
    /// <summary>Writes one member: nothing happens when it already equals <paramref name="value"/> by <see cref="Equality.Is{T}(T, T)"/>.</summary>
    public void Set<TValue>(StoreField<TState, TValue> field, TValue value)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!Equality.Is(field.Get(state), value))
        {
            SetState(field.With(state, value));
        }
    }

    // UTL-STA-12
    /// <summary>Replaces the record with a copy holding the same values and notifies, e.g. when a nested store changed.</summary>
    public void NotifyAll()
    {
        SetState(TState.Copy(state));
    }

    // UTL-STA-17
    /// <summary>Evaluates a named selector against the current record without subscribing.</summary>
    public TValue Select<TValue>(Func<TState, TValue> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return selector(state);
    }

    // UTL-STA-17
    /// <summary>Evaluates a named selector with an argument against the current record without subscribing.</summary>
    public TValue Select<TArg, TValue>(Func<TState, TArg, TValue> selector, TArg arg)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return selector(state, arg);
    }

    // UTL-STA-24
    /// <summary>
    /// Observes a selection: calls <paramref name="listener"/> at once with <c>(current, current, store)</c>,
    /// then after every notification whose selection differs by <see cref="Equality.Is{T}(T, T)"/> with
    /// <c>(new, previous, store)</c>. Disposing the result stops it.
    /// </summary>
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
