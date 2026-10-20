namespace Blazix.Primitives.Store;

/// <summary>
/// <c>useSyncedValueWithCleanup</c> (UTL-STA-20): one per call site, held by the part. <see cref="Sync"/> on every parameter
/// set writes the value as <see cref="ComponentStore{TState}.SyncValue{TValue}"/> does; when the value or the store changed
/// since the last call it first resets the member to <c>default</c> in the previous store. <see cref="Dispose"/> (from the
/// part's own dispose) resets it in the last store.
/// </summary>
public sealed class SyncedValueWithCleanup<TState, TValue> : IDisposable
    where TState : class, IStoreState<TState>
{
    private readonly StoreField<TState, TValue?> field;
    private ComponentStore<TState>? store;
    private TValue? value;

    /// <summary>Creates the helper for one member, <paramref name="field"/>, whose reset value is <c>default</c>.</summary>
    public SyncedValueWithCleanup(StoreField<TState, TValue?> field)
    {
        ArgumentNullException.ThrowIfNull(field);
        this.field = field;
    }

    /// <summary>
    /// The first call writes <paramref name="value"/> into <paramref name="store"/> when the member is not strictly equal
    /// to it. A later call with the same store and a value equal by <see cref="Equality.Is{T}(T, T)"/> does nothing; with a
    /// different store or value it first sets the member to <c>default</c> in the previous store, then writes as above.
    /// </summary>
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
    /// <remarks>Sets the member to <c>default</c> in the last store passed to <see cref="Sync"/>, then forgets that store, so a second call does nothing.</remarks>
    public void Dispose()
    {
        store?.Set(field, default);
        store = null;
    }
}
