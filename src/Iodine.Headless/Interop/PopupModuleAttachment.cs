using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 09: the twin-attachment helper every popup family's parts share (tracks 6, 7 and 8), in Interop/ beside its twin
// (settled by the orchestrator 2026-10-28, track 5's early review item 7).
// ADR 04 Amendments 2026-10-27 (the `popup` row): one PopupTwin per part, attached after the render that created the part's
// element and, under a portal, after the portal's move (design point 3 of M4 track 1); an unchanged options record sends
// nothing (ADR 16 rule 7). A render during the pending first attach awaits it, as TwinBase hands a repeated attach the
// pending handle; "attached" is signalled only by the attach that started it (issue 132).
/// <summary>Attaches a part's <c>popup</c> handle and sends its options when they changed.</summary>
internal sealed class PopupModuleAttachment : IAsyncDisposable
{
    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TwinHandle<PopupOptions>? handle;
    private PopupOptions? sent;
    private Task? attaching;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="PopupModuleAttachment"/> class.</summary>
    /// <param name="jsRuntime">The JavaScript runtime.</param>
    public PopupModuleAttachment(IJSRuntime jsRuntime)
    {
        Twin = new PopupTwin(jsRuntime);
    }

    /// <summary>Gets the part's twin, whose reports the part subscribes to.</summary>
    public PopupTwin Twin { get; }

    /// <summary>Gets a task that completes when the handle has attached the first time.</summary>
    public Task Attached => attached.Task;

    /// <summary>Gets the number of option records sent after the attach.</summary>
    public int Updates { get; private set; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await Twin.DisposeAsync();
    }

    /// <summary>Attaches the handle to <paramref name="element"/>, or sends <paramref name="options"/> when they changed.</summary>
    /// <param name="element">The part's element.</param>
    /// <param name="options">The options of this render.</param>
    /// <returns>A task that completes when the attach or the update was sent.</returns>
    public async Task SyncAsync(ElementReference element, PopupOptions options)
    {
        if (disposed)
        {
            return;
        }

        if (handle is null)
        {
            attaching ??= AttachAsync(element, options);
            await attaching;
        }

        if (handle is not null && options != sent)
        {
            sent = options;
            Updates++;
            await handle.UpdateAsync(options);
        }
    }

    // A part that leaves while its first attach is in flight gets a no-op handle from TwinBase (ADR 04 Amendments
    // 2026-10-29, gap 17 of M4 track 5).
    private async Task AttachAsync(ElementReference element, PopupOptions options)
    {
        sent = options;
        handle = await Twin.AttachAsync(element, options);
        attaching = null;
        attached.TrySetResult();
    }
}
