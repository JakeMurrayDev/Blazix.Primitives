namespace Blazix.Primitives;

// TST-API-03 (ToastProvider.tsx:34-57), TST-EVT-06, TST-EDGE-05; ADR 05 Amendments 2026-11-18 item 3 (chosen by the user
// 2026-11-18): one Provider's subscription to one manager. `invoke` is the Provider's InvokeAsync, which runs the work at
// once when called on the renderer's context, so a call made there reaches the store synchronously, and otherwise posts it
// there; a promise handled on the context returns the store's handled task, and a posted one a task that waits for it.
// The manager's Changed event is raised after each change of this store's list.
internal sealed class ToastProviderLink<TData> : IToastManagerListener<TData>, IDisposable
{
    private readonly ToastStore<TData> store;
    private readonly Func<Action, Task> invoke;
    private readonly IDisposable subscription;
    private readonly IDisposable observation;

    public ToastProviderLink(ToastManager<TData> manager, ToastStore<TData> store, Func<Action, Task> invoke)
    {
        this.store = store;
        this.invoke = invoke;
        observation = store.Observe(ToastSelectors<TData>.Toasts, (current, previous, _) =>
        {
            if (!ReferenceEquals(current, previous))
            {
                manager.RaiseChanged();
            }
        });
        subscription = manager.Subscribe(this);
    }

    /// <inheritdoc />
    public IReadOnlyList<ToastObject<TData>> Toasts => store.State.Toasts;

    /// <inheritdoc />
    public void Dispose()
    {
        subscription.Dispose();
        observation.Dispose();
    }

    /// <inheritdoc />
    public void Add(ToastObject<TData> toast) => _ = invoke(() => store.AddToast(toast));

    /// <inheritdoc />
    public void Update(string id, Func<ToastObject<TData>, ToastObject<TData>> update) => _ = invoke(() => store.UpdateToast(id, update));

    /// <inheritdoc />
    public void Close(string? id) => _ = invoke(() => store.CloseToast(id));

    /// <inheritdoc />
    public Task<TValue> Promise<TValue>(Task<TValue> task, ToastPromiseOptions<TValue, TData> options)
    {
        Task<TValue>? handled = null;
        Task invoked = invoke(() => handled = store.PromiseToast(task, options));
        return handled ?? Posted(invoked, () => handled!);
    }

    /// <inheritdoc />
    public Task Promise(Task task, ToastPromiseOptions<NoPayload, TData> options)
    {
        Task? handled = null;
        Task invoked = invoke(() => handled = store.PromiseToast(task, options));
        return handled ?? Posted(invoked, () => handled!);
    }

    private static async Task<TValue> Posted<TValue>(Task invoked, Func<Task<TValue>> handled)
    {
        await invoked.ConfigureAwait(false);
        return await handled().ConfigureAwait(false);
    }

    private static async Task Posted(Task invoked, Func<Task> handled)
    {
        await invoked.ConfigureAwait(false);
        await handled().ConfigureAwait(false);
    }
}
