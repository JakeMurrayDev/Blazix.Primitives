using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// UTL §4.8 through ScrollLockTwin (ADR 04 Amendments 2026-10-27, the scroll-lock bullet): Dialog's useScrollLock(open &&
// modal === true, popupElement) and, through PSH's anchored decision, Popover's. The anchored decision (PSH-PTR-07,
// PSH-ALG-17) is track 5's; this behavior takes the decided Enabled and the reference element (tracks 5 and 8).
/// <summary>Holds a popup's page scroll lock while enabled, on the document of its reference element.</summary>
internal sealed class PopupScrollLockBehavior : IAsyncDisposable
{
    private readonly ScrollLockTwin twin;
    private TwinHandle<ScrollLockOptions>? handle;
    private bool sentEnabled;

    /// <summary>Initializes a new instance of the <see cref="PopupScrollLockBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
    public PopupScrollLockBehavior(IJSRuntime jsRuntime)
    {
        twin = new ScrollLockTwin(jsRuntime);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // The twin disposes its live handles. Disposing it first, rather than awaiting a handle whose attach is still in
        // flight, marks it disposed with the part, so an attach the close overtook drops its missing element (issue 134's
        // class; a render during the pending attach receives that pending handle).
        handle = null;
        await twin.DisposeAsync();
    }

    /// <summary>Attaches to the reference element once the lock is enabled, and sends a change of <paramref name="enabled"/>.</summary>
    /// <param name="referenceElement">The element whose document is locked, or <see langword="null"/> before it renders.</param>
    /// <param name="enabled">Whether the popup locks page scroll now.</param>
    /// <returns>A task that completes when the module has the new state.</returns>
    /// <remarks>Call it from the owning part's <c>OnAfterRenderAsync</c>.</remarks>
    public async Task ApplyAsync(ElementReference? referenceElement, bool enabled)
    {
        if (handle is null)
        {
            if (!enabled || referenceElement is not { } element)
            {
                return;
            }

            handle = await twin.AttachAsync(element, new ScrollLockOptions { Enabled = true });
            sentEnabled = true;
            return;
        }

        if (sentEnabled != enabled)
        {
            sentEnabled = enabled;
            await handle.UpdateAsync(new ScrollLockOptions { Enabled = enabled });
        }
    }
}
