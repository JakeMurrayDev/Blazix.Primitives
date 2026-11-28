using System.Runtime.CompilerServices;
using Microsoft.JSInterop;

namespace Iodine.Headless.Interop;

// ADR 04 Amendments 2026-11-28 (issue 179, design point 10-179a (a), chosen by the user 2026-11-28): on Server a module is
// imported once per circuit, not once per twin, so a part created after its module was first imported attaches without the
// import's round trip. The imports are keyed under the runtime first, as InteractionsSessionBehavior keys its sessions (ADR 04
// Amendments 2026-11-11), so nothing crosses circuits; an entry lives as long as its circuit. Non-generic, so the twins of
// every type share one import of a module.
// Design point 10-179b (a), chosen by the user 2026-11-28: the import keeps the per-twin shape it had. Its interop call has
// one continuation, and each twin that asks while it is pending waits on a completion of its own, completed inline in the
// order the twins asked, so a twin resumes as it did when it awaited its own import. A second continuation on a shared task
// was queued instead of inlined, which on WebAssembly waits for a timer (a paused test clock never ran it).
/// <summary>The modules the twins of one Server circuit have imported, by URL: one import per module per circuit.</summary>
internal static class TwinModuleCache
{
    private static readonly ConditionalWeakTable<IJSRuntime, Dictionary<string, ModuleImport>> s_modules = new();

    /// <summary>Gets a value indicating whether the imports of <paramref name="jsRuntime"/> are shared by its twins.</summary>
    /// <param name="jsRuntime">The runtime of the circuit or the WebAssembly application.</param>
    /// <returns><see langword="true"/> on Server, where a twin leaves the import to the circuit; <see langword="false"/> on WebAssembly.</returns>
    public static bool IsShared(IJSRuntime jsRuntime) => jsRuntime is not IJSInProcessRuntime;

    /// <summary>Returns the circuit's import of a module, starting it on the first request; on WebAssembly, an import of the caller's own.</summary>
    /// <param name="jsRuntime">The runtime of the circuit or the WebAssembly application.</param>
    /// <param name="modulePath">The module URL.</param>
    /// <returns>
    /// The import: the module's reference, or <see langword="null"/> when the circuit was gone. A failed import is not kept,
    /// so the next request imports again.
    /// </returns>
    public static Task<IJSObjectReference?> ImportAsync(IJSRuntime jsRuntime, string modulePath)
    {
        // Design point 10-179c (a), chosen by the user 2026-11-28: on WebAssembly every twin imports for itself. An import of
        // a loaded module is an in-process call there, with no round trip to save, and a cached import would let the attach
        // run inside the part's after-render pass, which the drawer and menu-filter handles do not expect.
        if (!IsShared(jsRuntime))
        {
            return ImportOnceAsync(jsRuntime, modulePath);
        }

        Dictionary<string, ModuleImport> modules = s_modules.GetValue(jsRuntime, static _ => new(StringComparer.Ordinal));
        ModuleImport started;
        Task<IJSObjectReference?> result;
        lock (modules)
        {
            if (modules.TryGetValue(modulePath, out ModuleImport? import))
            {
                if (import.Completed is { } completed)
                {
                    return completed;
                }

                TaskCompletionSource<IJSObjectReference?> waiter = new();
                import.Waiters.Add(waiter);
                return waiter.Task;
            }

            started = new ModuleImport();
            TaskCompletionSource<IJSObjectReference?> first = new();
            started.Waiters.Add(first);
            modules[modulePath] = started;
            result = first.Task;
        }

        _ = CompleteAsync(jsRuntime, modulePath, modules, started);
        return result;
    }

    private static async Task<IJSObjectReference?> ImportOnceAsync(IJSRuntime jsRuntime, string modulePath)
    {
        try
        {
            return await jsRuntime.InvokeAsync<IJSObjectReference>("import", modulePath).ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    private static async Task CompleteAsync(IJSRuntime jsRuntime, string modulePath, Dictionary<string, ModuleImport> modules, ModuleImport import)
    {
        IJSObjectReference? reference = null;
        Exception? failure = null;
        try
        {
            reference = await jsRuntime.InvokeAsync<IJSObjectReference>("import", modulePath).ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; every later call is a no-op, so the null stays.
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        TaskCompletionSource<IJSObjectReference?>[] waiters;
        lock (modules)
        {
            if (failure is null)
            {
                import.Completed = Task.FromResult(reference);
            }
            else if (modules.TryGetValue(modulePath, out ModuleImport? kept) && ReferenceEquals(kept, import))
            {
                modules.Remove(modulePath);
            }

            waiters = [.. import.Waiters];
            import.Waiters.Clear();
        }

        // In the order the twins asked, each continuation running inline, outside the lock.
        foreach (TaskCompletionSource<IJSObjectReference?> waiter in waiters)
        {
            if (failure is null)
            {
                waiter.SetResult(reference);
            }
            else
            {
                waiter.SetException(failure);
            }
        }
    }

    // One module's import in one runtime: its result once it succeeded, and the twins waiting for it until then.
    private sealed class ModuleImport
    {
        public Task<IJSObjectReference?>? Completed { get; set; }

        public List<TaskCompletionSource<IJSObjectReference?>> Waiters { get; } = [];
    }
}
