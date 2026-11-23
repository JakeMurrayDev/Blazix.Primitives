using Iodine.Headless.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// ADR 04 Amendments 2026-11-18 (the `scroll-area` scope cell) with design point 6 (a) of the ScrollArea track: each part
// owns one handle of the `scroll-area` module on its element, given the root element as its one reference. The handover
// (ADR 04 Amendments 2026-10-18): a part whose style the module writes renders its full style until the attach and freezes
// that string from then on, so Blazor never writes over the module; it sends its own declarations and the consumer's
// string as `Style` on every update. A new element (a scrollbar or a corner shown again) is a new handle, unfrozen until it
// attaches. A part without an element (a substitute that leaves none) attaches nothing and is inert (SCR-EDGE-10).
/// <summary>One scroll area part's handle of the browser-side behavior, and the style it renders under the handover.</summary>
internal sealed class ScrollAreaHandle
{
    private readonly bool handsOverStyle;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TwinHandle<ScrollAreaOptions>? handle;
    private Task? attaching;
    private ElementReference? attachedElement;
    private ScrollAreaOptions? sentOptions;
    private ElementReference? sentRoot;
    private bool frozen;
    private string? frozenStyle;
    private string? renderedStyle;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ScrollAreaHandle"/> class.</summary>
    /// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
    /// <param name="handsOverStyle">Whether the module writes the part's style, so its rendered style freezes at the attach.</param>
    public ScrollAreaHandle(IJSRuntime jsRuntime, bool handsOverStyle)
    {
        Twin = new ScrollAreaTwin(jsRuntime);
        this.handsOverStyle = handsOverStyle;
    }

    /// <summary>Gets the part's twin.</summary>
    /// <value>Created with the handle.</value>
    public ScrollAreaTwin Twin { get; }

    /// <summary>Gets a task that completes when the part's first handle has attached.</summary>
    /// <value>A task that never faults.</value>
    public Task Attached => attachedSource.Task;

    /// <summary>Starts the disposal of the twin and every handle it attached.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        attachedSource.TrySetResult();
        _ = Twin.DisposeAsync().AsTask();
    }

    /// <summary>Returns the style a render shows: the full style until the attach, the frozen one from then on.</summary>
    /// <param name="full">The part's declarations, the module's initial values included, merged with the consumer's style.</param>
    /// <returns>The style to render.</returns>
    public string? RenderStyle(string? full)
    {
        if (frozen)
        {
            return frozenStyle;
        }

        renderedStyle = full;
        return full;
    }

    /// <summary>Attaches a handle to a new element, disposes the handle of an element that left, or sends changed options.</summary>
    /// <param name="element">The part's element, or <see langword="null"/> when it renders none.</param>
    /// <param name="options">The options of this render.</param>
    /// <param name="root">The root's element, for every part but the root; <see langword="null"/> for the root.</param>
    /// <returns>A task that completes when the call was made.</returns>
    public async Task SyncAsync(ElementReference? element, ScrollAreaOptions options, ElementReference? root)
    {
        if (disposed)
        {
            return;
        }

        if (!Nullable.Equals(element, attachedElement))
        {
            TwinHandle<ScrollAreaOptions>? released = handle;
            handle = null;
            attaching = null;
            sentOptions = null;
            frozen = false;
            frozenStyle = null;
            attachedElement = element;
            if (released is not null)
            {
                await released.DisposeAsync();
            }
        }

        if (element is not { } target)
        {
            return;
        }

        TwinReferences? references = null;
        if (root is { } rootElement)
        {
            references = new TwinReferences();
            references.Add(rootElement);
        }

        if (handle is null)
        {
            // A render while the first attach is in flight (the first measurement can arrive before it completes, on
            // WebAssembly) waits for it and then sends its own options: the twin returns the in-flight handle to a second
            // attach of the same element, so the newer options would otherwise be lost.
            if (attaching is null)
            {
                // The handover: what this element shows now stays rendered from the attach on.
                if (handsOverStyle)
                {
                    frozen = true;
                    frozenStyle = renderedStyle;
                }

                sentOptions = options;
                sentRoot = root;
                attaching = AttachAsync(target, options, references);
            }

            await attaching;
            if (handle is null || !Nullable.Equals(element, attachedElement))
            {
                return;
            }
        }

        if (options != sentOptions || !Nullable.Equals(root, sentRoot))
        {
            sentOptions = options;
            sentRoot = root;
            await handle.UpdateAsync(options, references);
        }
    }

    private async Task AttachAsync(ElementReference target, ScrollAreaOptions options, TwinReferences? references)
    {
        TwinHandle<ScrollAreaOptions> attached = await Twin.AttachAsync(target, options, references);
        if (Nullable.Equals(attachedElement, target))
        {
            handle = attached;
        }
        else
        {
            // The element left while its attach was in flight.
            await attached.DisposeAsync();
        }

        attachedSource.TrySetResult();
    }
}
