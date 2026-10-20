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
    // Insertion-ordered and iterated live (UTL §8.2): a listener removed during a pass becomes a null tombstone so the
    // pass skips it, and one added during a pass is appended and reached. Tombstones are compacted when no pass runs.
    private readonly List<Action<TState>?> listeners = [];
    private TState state;
    private int updateTick;
    private int notifyDepth;
    private bool hasTombstones;

    public Store(TState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        this.state = state;
    }

    /// <summary>The current record (UTL-STA-07). Never mutate it; write through the store.</summary>
    public TState State => state;

    /// <summary>
    /// Registers <paramref name="listener"/> (UTL-STA-13): it runs after every write with the new record. Registering the
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

    /// <summary>Replaces the record and notifies, unless <paramref name="next"/> is the current record (UTL-STA-08, UTL-ALG-03).</summary>
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

    /// <summary>
    /// Writes <c>change(State)</c> (ADR 05 §1's <c>Update(state =&gt; state with { … })</c>) with one notification, unless
    /// every member is equal to the current one by <see cref="Equality.Is{T}(T, T)"/> (UTL-STA-11).
    /// </summary>
    public void Update(Func<TState, TState> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Write(change(state));
    }

    /// <summary>The closure-free form of <see cref="Update(Func{TState, TState})"/>.</summary>
    public void Update<TArg>(Func<TState, TArg, TState> change, TArg arg)
    {
        ArgumentNullException.ThrowIfNull(change);
        Write(change(state, arg));
    }

    /// <summary>Writes one member (UTL-STA-10): nothing happens when it already equals <paramref name="value"/> by <see cref="Equality.Is{T}(T, T)"/>.</summary>
    public void Set<TValue>(StoreField<TState, TValue> field, TValue value)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!Equality.Is(field.Get(state), value))
        {
            SetState(field.With(state, value));
        }
    }

    /// <summary>Replaces the record with a copy holding the same values and notifies (UTL-STA-12), e.g. when a nested store changed.</summary>
    public void NotifyAll()
    {
        SetState(TState.Copy(state));
    }

    /// <summary>Evaluates a named selector against the current record without subscribing (UTL-STA-17).</summary>
    public TValue Select<TValue>(Func<TState, TValue> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return selector(state);
    }

    /// <summary>Evaluates a named selector with an argument against the current record without subscribing (UTL-STA-17).</summary>
    public TValue Select<TArg, TValue>(Func<TState, TArg, TValue> selector, TArg arg)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return selector(state, arg);
    }

    /// <summary>
    /// Observes a selection (UTL-STA-24): calls <paramref name="listener"/> at once with <c>(current, current, store)</c>,
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
        public void Dispose() => store.Unsubscribe(listener);
    }
}
