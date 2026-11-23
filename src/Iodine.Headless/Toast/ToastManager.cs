namespace Iodine.Headless;

// TST §3.2 (createToastManager.ts:12-104, useToastManager.ts:9-24); TST-API-05..10, TST-EVT-06, TST-EVT-11; ADR 05
// Amendments 2026-10-18 item 3 (a class with createToastManager's methods and a subscription event, not a store) and
// 2026-11-18 item 3 (chosen by the user 2026-11-18): registered scoped, the Provider's cascaded instance is the
// useToastManager equivalent, a call off the renderer's context is marshalled through each subscribed Provider, the id
// returned at once, and a public Changed event. The Provider's subscription (Base UI's ' subscribe', TST-API-10) is
// internal. Events reach every Provider subscribed at the moment of the call, synchronously when the call is made on that
// Provider's context; a manager with no Provider discards them (TST-API-08).
/// <summary>Shows, updates and closes toasts, and tracks the toasts shown.</summary>
/// <typeparam name="TData">The type of the custom data the toasts carry.</typeparam>
/// <remarks>
/// <para>
/// A toast provider acts on the calls of the manager it is given, of the manager registered for the application, or of
/// a manager of its own, and cascades that manager to its content. Several providers may share one manager: each shows
/// every toast added through it.
/// </para>
/// <para>
/// The methods can be called from any thread. A call made away from a provider's renderer is applied on that renderer;
/// the id <see cref="Add(ToastObject{TData})"/> returns is valid at once. A call made while no provider is rendered
/// interactively, including during prerendering, has no effect.
/// </para>
/// </remarks>
public class ToastManager<TData>
{
    private static readonly IReadOnlyList<ToastObject<TData>> s_noToasts = [];

    private readonly Lock gate = new();
    private IToastManagerListener<TData>[] listeners = [];

    /// <summary>Occurs after the toasts of a provider subscribed to this manager change.</summary>
    /// <remarks>The event is raised on that provider's renderer.</remarks>
    public event EventHandler? Changed;

    /// <summary>Gets the toasts shown, newest first.</summary>
    /// <value>
    /// The toasts of the first provider subscribed to this manager, or an empty list while none is. The list is replaced,
    /// never changed, on every change; <see cref="Changed"/> is raised after each.
    /// </value>
    public IReadOnlyList<ToastObject<TData>> Toasts
    {
        get
        {
            IToastManagerListener<TData>[] current = Volatile.Read(ref listeners);
            return current.Length > 0 ? current[0].Toasts : s_noToasts;
        }
    }

    // TST-API-06, TST-API-08: the id is drawn here, before any provider handles the add.
    /// <summary>Shows a toast, or updates the toast shown under the same id.</summary>
    /// <param name="toast">
    /// The toast. When its <see cref="ToastObject{TData}.Id"/> names a toast that is shown and not closing, that toast is
    /// updated with the properties the initializer set, and its auto-dismiss delay restarts.
    /// </param>
    /// <returns>The toast's id: the one given, or a generated one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="toast"/> is <see langword="null"/>.</exception>
    public string Add(ToastObject<TData> toast)
    {
        ArgumentNullException.ThrowIfNull(toast);
        string id = toast.Id.Length > 0 ? toast.Id : ToastIds.Next();
        ToastObject<TData> added = toast.Id.Length > 0 ? toast : toast with { Id = id };
        foreach (IToastManagerListener<TData> listener in Volatile.Read(ref listeners))
        {
            listener.Add(added);
        }

        return id;
    }

    /// <summary>Changes a toast.</summary>
    /// <param name="id">The toast's id.</param>
    /// <param name="update">
    /// The function, called once with the current toast, that returns the change: typically a <c>with</c> expression that
    /// assigns the properties to change. It is not called when the toast is not shown or is closing.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> or <paramref name="update"/> is <see langword="null"/>.</exception>
    public void Update(string id, Func<ToastObject<TData>, ToastObject<TData>> update)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(update);
        foreach (IToastManagerListener<TData> listener in Volatile.Read(ref listeners))
        {
            listener.Update(id, update);
        }
    }

    /// <summary>Closes a toast, or every toast.</summary>
    /// <param name="id">The toast's id, or <see langword="null"/> (the default) to close every toast.</param>
    public void Close(string? id = null)
    {
        foreach (IToastManagerListener<TData> listener in Volatile.Read(ref listeners))
        {
            listener.Close(id);
        }
    }

    // TST-API-09: the provider's handled task when a provider took the call; otherwise the task itself.
    /// <summary>Shows a toast that follows a task: a loading state while it runs, then a success or an error state.</summary>
    /// <typeparam name="TValue">The type of the task's result.</typeparam>
    /// <param name="task">The task to follow.</param>
    /// <param name="options">The toast's three states.</param>
    /// <returns>
    /// A task that completes with the result after the success state is applied, or fails with the same exception after
    /// the error state is applied; a canceled <paramref name="task"/> takes the error state. While no provider is
    /// subscribed, <paramref name="task"/> itself.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="task"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public Task<TValue> Promise<TValue>(Task<TValue> task, ToastPromiseOptions<TValue, TData> options)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(options);
        Task<TValue> handled = task;
        foreach (IToastManagerListener<TData> listener in Volatile.Read(ref listeners))
        {
            handled = listener.Promise(task, options);
        }

        return handled;
    }

    /// <summary>Shows a toast that follows a task with no result: a loading state while it runs, then a success or an error state.</summary>
    /// <param name="task">The task to follow.</param>
    /// <param name="options">The toast's three states; the success state's argument carries no value.</param>
    /// <returns>
    /// A task that completes after the success state is applied, or fails with the same exception after the error state is
    /// applied; a canceled <paramref name="task"/> takes the error state. While no provider is subscribed,
    /// <paramref name="task"/> itself.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="task"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public Task Promise(Task task, ToastPromiseOptions<NoPayload, TData> options)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(options);
        Task handled = task;
        foreach (IToastManagerListener<TData> listener in Volatile.Read(ref listeners))
        {
            handled = listener.Promise(task, options);
        }

        return handled;
    }

    // TST-API-10, TST-API-03, TST-EDGE-05: the Provider's subscription. Copy on write, so a call from another thread reads a
    // consistent array.
    internal IDisposable Subscribe(IToastManagerListener<TData> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (gate)
        {
            if (Array.IndexOf(listeners, listener) < 0)
            {
                Volatile.Write(ref listeners, [.. listeners, listener]);
            }
        }

        return new Subscription(this, listener);
    }

    internal void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private void Unsubscribe(IToastManagerListener<TData> listener)
    {
        lock (gate)
        {
            int index = Array.IndexOf(listeners, listener);
            if (index >= 0)
            {
                IToastManagerListener<TData>[] next = new IToastManagerListener<TData>[listeners.Length - 1];
                Array.Copy(listeners, 0, next, 0, index);
                Array.Copy(listeners, index + 1, next, index, listeners.Length - index - 1);
                Volatile.Write(ref listeners, next);
            }
        }
    }

    private sealed class Subscription(ToastManager<TData> manager, IToastManagerListener<TData> listener) : IDisposable
    {
        private ToastManager<TData>? owner = manager;

        /// <inheritdoc />
        public void Dispose()
        {
            Interlocked.Exchange(ref owner, null)?.Unsubscribe(listener);
        }
    }
}

// ADR 08 Amendments 2026-11-18 (the non-generic default over NoPayload, chosen by the user 2026-11-18); ADR 05 Amendments
// 2026-11-18 item 3 (the type AddIodineHeadless registers, scoped).
/// <summary>Shows, updates and closes toasts that carry no custom data, and tracks the toasts shown.</summary>
/// <remarks>
/// <para>
/// <c>AddIodineHeadless</c> registers one instance per scope: per circuit on the server, per application in the
/// browser. Inject it anywhere to show toasts; a toast provider without a manager of its own uses it.
/// </para>
/// </remarks>
public sealed class ToastManager : ToastManager<NoPayload>;

// TST-API-10: what a Provider subscribes to a manager with.
internal interface IToastManagerListener<TData>
{
    IReadOnlyList<ToastObject<TData>> Toasts { get; }

    void Add(ToastObject<TData> toast);

    void Update(string id, Func<ToastObject<TData>, ToastObject<TData>> update);

    void Close(string? id);

    Task<TValue> Promise<TValue>(Task<TValue> task, ToastPromiseOptions<TValue, TData> options);

    Task Promise(Task task, ToastPromiseOptions<NoPayload, TData> options);
}
