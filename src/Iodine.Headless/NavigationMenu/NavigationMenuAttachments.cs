using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ADR 04 Amendments 2026-11-03 (4): every part that owns a `navigation-menu` handle attaches it after the render that
// created its element (and, under a portal, after the portal's move), with the Root's element as the first reference, so
// the module joins the part to its menu (the PopupModuleAttachment shape: an unchanged options record sends nothing, ADR 16
// rule 7; a render during the pending first attach awaits it; a part that leaves while it attaches gets TwinBase's no-op
// handle, issue 132).
/// <summary>Attaches one part's <c>navigation-menu</c> handle and sends its option changes.</summary>
internal sealed class NavigationMenuModule : IAsyncDisposable
{
    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TwinHandle<NavigationMenuOptions>? handle;
    private NavigationMenuOptions? sent;
    private Task? attaching;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuModule"/> class.</summary>
    /// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
    public NavigationMenuModule(IJSRuntime jsRuntime)
    {
        Twin = new NavigationMenuTwin(jsRuntime);
    }

    /// <summary>Gets the part's twin, whose events the part subscribes to.</summary>
    public NavigationMenuTwin Twin { get; }

    /// <summary>Gets a task that completes once the handle has attached the first time.</summary>
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

    /// <summary>Attaches the handle, or sends the options when they changed.</summary>
    /// <param name="element">The part's element.</param>
    /// <param name="options">The options; the root reference is filled in for every part but the root.</param>
    /// <param name="root">The root's element, or <see langword="null"/> for the root's own handle.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task SyncAsync(ElementReference element, NavigationMenuOptions options, ElementReference? root)
    {
        if (root is not null)
        {
            options = options with { RootRef = 0 };
        }

        while (!disposed)
        {
            if (attaching is { IsCompleted: false } pending)
            {
                // A render during the pending attach waits for it, then checks the element again.
                await pending;
                continue;
            }

            if (handle is not null && !string.Equals(handle.Key, element.Id, StringComparison.Ordinal))
            {
                // The part's element was replaced (a Content that mounts again): the module lets go of the old one
                // (FocusManagerBehavior's precedent).
                TwinHandle<NavigationMenuOptions> previous = handle;
                handle = null;
                sent = null;
                await previous.DisposeAsync();
            }

            if (handle is null)
            {
                attaching = AttachAsync(element, options, root);
                await attaching;
                return;
            }

            if (options != sent)
            {
                sent = options;
                await handle.UpdateAsync(options, References(root));
            }

            return;
        }
    }

    private static TwinReferences? References(ElementReference? root)
    {
        if (root is not { } element)
        {
            return null;
        }

        TwinReferences references = new();
        references.Add(element);
        return references;
    }

    private async Task AttachAsync(ElementReference element, NavigationMenuOptions options, ElementReference? root)
    {
        try
        {
            handle = await Twin.AttachAsync(element, options, References(root));
        }
        catch (JSException)
        {
            // The element left the document before the attach arrived (a Content closed while the module loaded): there is
            // nothing to attach to, and the next render with an element attaches again (FocusManagerBehavior's and
            // CollapsiblePanelBehavior's precedent). An exception here would end a Server circuit.
            return;
        }

        sent = options;
        attached.TrySetResult();
    }
}

// The `transitions` handle of an animated part (the Popup, the Backdrop, the Positioner, each Content, the inline Viewport
// target), the PopoverPopup shape: the options are read once the module is imported, right before the attach (issue 147
// (B1)), so a close retained while the import was in flight is in them; a later retain is announced through the module's
// export, addressed by the element.
/// <summary>Attaches one part's <c>transitions</c> handle and sends its option changes.</summary>
internal sealed class NavigationMenuTransitions : IAsyncDisposable
{
    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TwinHandle<TransitionsOptions>? handle;
    private TransitionsOptions? sent;
    private Task? attaching;
    private bool announcedRetain;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuTransitions"/> class.</summary>
    /// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
    public NavigationMenuTransitions(IJSRuntime jsRuntime)
    {
        Twin = new TransitionsTwin(jsRuntime);
    }

    /// <summary>Gets the part's twin, whose events the part subscribes to.</summary>
    public TransitionsTwin Twin { get; }

    /// <summary>Gets a task that completes once the handle has attached the first time.</summary>
    public Task Attached => attached.Task;

    /// <summary>Gets a value indicating whether the handle has attached or is attaching, so the part's style is the module's.</summary>
    public bool HandedOver => handle is not null || attaching is { IsCompleted: false };

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

    /// <summary>Attaches the handle, or sends the options when they changed.</summary>
    /// <param name="element">The part's element.</param>
    /// <param name="options">Builds the options, right before the attach and on each later render.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task SyncAsync(ElementReference element, Func<TransitionsOptions> options)
    {
        while (!disposed)
        {
            if (attaching is { IsCompleted: false } pending)
            {
                // A render during the pending attach waits for it, then checks the element again.
                await pending;
                continue;
            }

            if (handle is not null && !string.Equals(handle.Key, element.Id, StringComparison.Ordinal))
            {
                // The part's element was replaced (a Content that mounts again): the module lets go of the old one.
                TwinHandle<TransitionsOptions> previous = handle;
                handle = null;
                sent = null;
                announcedRetain = false;
                await previous.DisposeAsync();
            }

            if (handle is null)
            {
                attaching = AttachAsync(element, options);
                await attaching;
                return;
            }

            TransitionsOptions next = options();
            if (next != sent)
            {
                sent = next;
                await handle.UpdateAsync(next);
            }

            return;
        }
    }

    // Issue 147 (B1).
    /// <summary>Announces a change of the retained close ahead of its render.</summary>
    /// <param name="element">The part's element, or <see langword="null"/> while it has none.</param>
    /// <param name="retain">Whether the close is retained.</param>
    public void AnnounceRetainExit(ElementReference? element, bool retain)
    {
        if (disposed || element is not { } target || (handle is null && attaching is not { IsCompleted: false }) || announcedRetain == retain)
        {
            return;
        }

        announcedRetain = retain;
        _ = Twin.RetainExitAsync(target, retain).AsTask();
    }

    private async Task AttachAsync(ElementReference element, Func<TransitionsOptions> options)
    {
        await Twin.PrepareAsync();
        if (disposed)
        {
            return;
        }

        TransitionsOptions first = options();
        try
        {
            handle = await Twin.AttachAsync(element, first);
        }
        catch (JSException)
        {
            // The element left the document before the attach arrived: the next render with an element attaches again.
            return;
        }

        sent = first;
        attached.TrySetResult();
    }
}
