namespace Blazix.Primitives;

// CMP §4.2.2, CMP-STA-19..21, CMP-ALG-30 (internals/useItemRegistry.ts:14-54): M5 track 12's design point 3 (a), settled by
// the orchestrator 2026-11-11 (the Composite/ grant, new files only). Registrations go into the live map at once and ask the
// owner, once per batch (the latch), to publish; the owner calls Publish at its batch boundary, which copies the live map
// into a new snapshot that is never mutated after. Menu's filter is the consumer (MenuFilterEngine's items).
/// <summary>Represents a registry of items by key that publishes one immutable snapshot per render batch.</summary>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TItem">The type of the items.</typeparam>
internal sealed class KeyedItemRegistry<TKey, TItem>
    where TKey : notnull
{
    private static readonly IReadOnlyDictionary<TKey, TItem> s_empty = new OrderedDictionary<TKey, TItem>();

    private readonly OrderedDictionary<TKey, TItem> live = [];
    private readonly Dictionary<TKey, Registration> owners = [];
    private readonly Action requestPublish;
    private bool publishRequested;

    /// <summary>Initializes a new instance of the <see cref="KeyedItemRegistry{TKey, TItem}"/> class.</summary>
    /// <param name="requestPublish">The callback that asks the owner to call <see cref="Publish"/> at its batch boundary; run at most once per batch.</param>
    public KeyedItemRegistry(Action requestPublish) => this.requestPublish = requestPublish ?? throw new ArgumentNullException(nameof(requestPublish));

    /// <summary>Gets the snapshot published at the latest batch boundary, in registration order.</summary>
    public IReadOnlyDictionary<TKey, TItem> Items { get; private set; } = s_empty;

    /// <summary>Gets the live registry, including registrations whose snapshot has not been published yet.</summary>
    public IReadOnlyDictionary<TKey, TItem> LiveItems => live;

    /// <summary>Gets the live items in registration order, as a list an owner can index without a copy.</summary>
    public IReadOnlyList<TItem> LiveValues => live.Values;

    /// <summary>Registers an item under a key, replacing the item a key already holds.</summary>
    /// <param name="key">The key.</param>
    /// <param name="item">The item.</param>
    /// <returns>The registration; disposing it removes the key unless a later registration holds it.</returns>
    public IDisposable Register(TKey key, TItem item)
    {
        Registration registration = new(this, key);
        live[key] = item;
        owners[key] = registration;
        RequestPublish();
        return registration;
    }

    /// <summary>Publishes a new snapshot when a registration changed since the last one; the owner calls it at its batch boundary.</summary>
    /// <returns><see langword="true"/> when a new snapshot was published.</returns>
    public bool Publish()
    {
        if (!publishRequested)
        {
            return false;
        }

        publishRequested = false;
        Items = live.Count == 0 ? s_empty : new OrderedDictionary<TKey, TItem>(live);
        return true;
    }

    private void RequestPublish()
    {
        if (publishRequested)
        {
            return;
        }

        publishRequested = true;
        requestPublish();
    }

    private void Unregister(Registration registration)
    {
        // CMP-STA-20: a register -> unregister -> register sequence leaves the key with its latest registration.
        if (owners.TryGetValue(registration.Key, out Registration? owner) && ReferenceEquals(owner, registration))
        {
            owners.Remove(registration.Key);
            live.Remove(registration.Key);
            RequestPublish();
        }
    }

    private sealed class Registration(KeyedItemRegistry<TKey, TItem> registry, TKey key) : IDisposable
    {
        private bool disposed;

        public TKey Key { get; } = key;

        /// <inheritdoc />
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                registry.Unregister(this);
            }
        }
    }
}
