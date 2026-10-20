namespace Blazix.Primitives.Store;

// UTL-STA-20
/// <summary>
/// Represents the copy of one parameter into a member of a store's record that resets the member when the part goes
/// away or the store changes (Base UI's <c>useSyncedValueWithCleanup</c>).
/// </summary>
/// <typeparam name="TState">The type of the immutable state record.</typeparam>
/// <typeparam name="TValue">The type of the member; its reset value is <see langword="default"/>.</typeparam>
/// <remarks>
/// Create one per call site and keep it in a field of the part. Call <see cref="Sync"/> on every parameter set and
/// <see cref="Dispose"/> from the part's own <see cref="IDisposable.Dispose"/>.
/// </remarks>
public sealed class SyncedValueWithCleanup<TState, TValue> : IDisposable
    where TState : class, IStoreState<TState>
{
    private readonly StoreField<TState, TValue?> field;
    private ComponentStore<TState>? store;
    private TValue? value;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncedValueWithCleanup{TState, TValue}"/> class for one member.
    /// </summary>
    /// <param name="field">The key of the member.</param>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> is <see langword="null"/>.</exception>
    public SyncedValueWithCleanup(StoreField<TState, TValue?> field)
    {
        ArgumentNullException.ThrowIfNull(field);
        this.field = field;
    }

    /// <summary>
    /// Copies <paramref name="value"/> into the member of <paramref name="store"/>'s record.
    /// </summary>
    /// <param name="store">The store the part currently holds.</param>
    /// <param name="value">The parameter's value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The first call writes <paramref name="value"/> when the member is not strictly equal to it. A later call with the same
    /// store and a value equal by <see cref="Equality.Is{T}(T, T)"/> does nothing. A later call with a different store or value
    /// first sets the member to <see langword="default"/> in the previous store, then writes as the first call does.
    /// </remarks>
    public void Sync(ComponentStore<TState> store, TValue? value)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (this.store is not null)
        {
            if (ReferenceEquals(this.store, store) && Equality.Is(this.value, value))
            {
                // Nothing the effect depends on changed, so it does not run again.
                return;
            }

            this.store.Set(field, default);
        }

        this.store = store;
        this.value = value;
        store.SyncValue(field, value);
    }

    /// <inheritdoc />
    /// <remarks>Sets the member to <see langword="default"/> in the last store passed to <see cref="Sync"/>, then forgets that store, so a second call does nothing.</remarks>
    public void Dispose()
    {
        store?.Set(field, default);
        store = null;
    }
}
