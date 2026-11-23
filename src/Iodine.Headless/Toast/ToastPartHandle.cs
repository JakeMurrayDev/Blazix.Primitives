using Iodine.Headless.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// ADR 04 Amendments 2026-11-18 (the `toast` scope cell) with DP1 (a) of the Toast track (chosen by the user 2026-11-19):
// each Toast part owns one `ToastTwin` and one handle on its element. The handover (ADR 04 Amendments 2026-10-18): a part
// whose style the module writes (the Root) renders its full style until the attach and freezes that string from then on,
// so Blazor never writes over the module; the part sends its merged style as `Style` with every update. An update is sent
// only when the options or the elements they name changed (DP1: a Viewport render reaches every Root, so a hover never
// costs one message per toast). The ScrollAreaHandle shape (M6 track 5).
/// <summary>One Toast part's handle of the browser-side behavior, and the style it renders under the handover.</summary>
internal sealed class ToastPartHandle
{
    private readonly bool handsOverStyle;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TwinHandle<ToastOptions>? handle;
    private Task? attaching;
    private ElementReference? attachedElement;
    private ToastOptions? sentOptions;
    private ElementReference[] sentReferences = [];
    private bool frozen;
    private string? frozenStyle;
    private string? renderedStyle;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ToastPartHandle"/> class.</summary>
    /// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
    /// <param name="handsOverStyle">Whether the module writes the part's style, so its rendered style freezes at the attach.</param>
    public ToastPartHandle(IJSRuntime jsRuntime, bool handsOverStyle)
    {
        Twin = new ToastTwin(jsRuntime);
        this.handsOverStyle = handsOverStyle;
    }

    /// <summary>Gets the part's twin.</summary>
    /// <value>Created with the handle.</value>
    public ToastTwin Twin { get; }

    /// <summary>Gets a task that completes when the part's first handle has attached.</summary>
    /// <value>A task that never faults.</value>
    public Task Attached => attachedSource.Task;

    /// <summary>Gets a value indicating whether the part's rendered style is frozen, from the attach on.</summary>
    /// <value><see langword="true"/> while the module writes the style.</value>
    public bool IsFrozen => frozen;

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
    /// <param name="options">The options of this render, whose reference fields index <paramref name="references"/>.</param>
    /// <param name="references">The elements the options name by index.</param>
    /// <returns>A task that completes when the call was made.</returns>
    public Task SyncAsync(ElementReference? element, ToastOptions options, ElementReference[] references)
    {
        // The steady state of a render that changed nothing the module reads sends nothing and allocates nothing.
        if (disposed || (handle is not null && SameElement(element, attachedElement) && options == sentOptions && SameReferences(references, sentReferences)))
        {
            return Task.CompletedTask;
        }

        return SyncCoreAsync(element, options, references);
    }

    /// <summary>Adds an element to a part's references.</summary>
    /// <param name="references">The references of this render.</param>
    /// <param name="element">The element, or <see langword="null"/>.</param>
    /// <returns>The element's index, or <see langword="null"/> when there is none.</returns>
    public static int? Reference(List<ElementReference> references, ElementReference? element)
    {
        if (element is not { } value)
        {
            return null;
        }

        for (int i = 0; i < references.Count; i++)
        {
            if (SameElement(references[i], value))
            {
                return i;
            }
        }

        references.Add(value);
        return references.Count - 1;
    }

    // An element reference is compared by its id and renderer context: the default struct equality reflects over its fields
    // and allocates (the PopupPortalBehavior precedent).
    /// <summary>Determines whether two element references name the same element.</summary>
    /// <param name="left">The first reference, or <see langword="null"/>.</param>
    /// <param name="right">The second reference, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are absent, or both name the same element.</returns>
    public static bool SameElement(ElementReference? left, ElementReference? right) =>
        left is { } l ? right is { } r && string.Equals(l.Id, r.Id, StringComparison.Ordinal) && ReferenceEquals(l.Context, r.Context) : right is null;

    private async Task SyncCoreAsync(ElementReference? element, ToastOptions options, ElementReference[] references)
    {
        if (!SameElement(element, attachedElement))
        {
            TwinHandle<ToastOptions>? released = handle;
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

        if (handle is null)
        {
            // A render while the first attach is in flight waits for it and then sends its own options: the twin returns the
            // in-flight handle to a second attach of the same element, so the newer options would otherwise be lost.
            if (attaching is null)
            {
                if (handsOverStyle)
                {
                    frozen = true;
                    frozenStyle = renderedStyle;
                }

                sentOptions = options;
                sentReferences = references;
                attaching = AttachAsync(target, options, ToReferences(references));
            }

            await attaching;
            if (handle is null || !SameElement(element, attachedElement))
            {
                return;
            }
        }

        if (options != sentOptions || !SameReferences(references, sentReferences))
        {
            sentOptions = options;
            sentReferences = references;
            await handle.UpdateAsync(options, ToReferences(references));
        }
    }

    private static TwinReferences? ToReferences(ElementReference[] references)
    {
        if (references.Length == 0)
        {
            return null;
        }

        TwinReferences list = new();
        foreach (ElementReference reference in references)
        {
            list.Add(reference);
        }

        return list;
    }

    private static bool SameReferences(ElementReference[] left, ElementReference[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (!SameElement(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private async Task AttachAsync(ElementReference target, ToastOptions options, TwinReferences? references)
    {
        TwinHandle<ToastOptions> attached = await Twin.AttachAsync(target, options, references);
        if (SameElement(attachedElement, target))
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
