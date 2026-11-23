using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The SelectModuleAttachment shape for the `menu-filter` module (ADR 04 §1's `menu-filter` row, M5 track 12's design point
// 1 (a)): one MenuFilterTwin per part, attached after the render that created the part's element and after the portal's
// move; an unchanged options record sends nothing (ADR 16 rule 7); a render during the pending first attach awaits it, then
// sends its own options when they differ; a part that leaves while its attach is in flight gets TwinBase's no-op handle
// (issue 132).
/// <summary>Attaches a filter part's <c>menu-filter</c> handle and sends its options when they changed.</summary>
internal sealed class MenuFilterModuleAttachment : IAsyncDisposable
{
    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TwinHandle<MenuFilterOptions>? handle;
    private MenuFilterOptions? sent;
    private Task? attaching;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="MenuFilterModuleAttachment"/> class.</summary>
    /// <param name="jsRuntime">The JavaScript runtime.</param>
    public MenuFilterModuleAttachment(IJSRuntime jsRuntime)
    {
        Twin = new MenuFilterTwin(jsRuntime);
    }

    /// <summary>Gets the part's twin, whose reports the part subscribes to.</summary>
    public MenuFilterTwin Twin { get; }

    /// <summary>Gets a task that completes when the handle has attached the first time.</summary>
    public Task Attached => attached.Task;

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
    public async Task SyncAsync(ElementReference element, MenuFilterOptions options)
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
            await handle.UpdateAsync(options);
        }
    }

    private async Task AttachAsync(ElementReference element, MenuFilterOptions options)
    {
        sent = options;
        handle = await Twin.AttachAsync(element, options);
        attaching = null;
        attached.TrySetResult();
    }
}
