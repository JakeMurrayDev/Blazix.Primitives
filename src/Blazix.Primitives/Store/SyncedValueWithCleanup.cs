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
    public SyncedValueWithCleanup(StoreField<TState, TValue?> field)
    {
        throw new NotImplementedException();
    }

    /// <summary>Synchronises <paramref name="value"/> into <paramref name="store"/>.</summary>
    public void Sync(ComponentStore<TState> store, TValue? value)
    {
        throw new NotImplementedException();
    }

    /// <summary>Resets the member to <c>default</c> in the last store.</summary>
    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
