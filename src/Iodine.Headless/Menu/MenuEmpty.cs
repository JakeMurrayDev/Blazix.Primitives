using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Menu.Empty (menu/empty/MenuEmpty.tsx, filter-dropdown/empty/FilterDropdownEmpty.tsx); the contract is
// docs/components/menu.md §3.24 (MNU-API-61, DOM-23, ARIA-16, STA-23). It decides only after its first render, once the
// items of the same batch have registered (FilterDropdownEmpty.tsx:25-31's ready flag), so its children never mount for
// a render while items match and it is absent from prerendered markup. Each time it appears it attaches a DomTwin with
// LiveRegion on its element, the initial live-region text mutation of UTL-A11Y-01..04 (useInitialLiveRegionTextMutation).
/// <summary>
/// Represents the message a filterable menu shows when no item matches, Base UI's <c>Menu.Empty</c>, rendered as a
/// <c>div</c> live region only while nothing is visible.
/// </summary>
/// <remarks>It must be placed in a menu wrapped in <see cref="MenuFilterProvider"/>.</remarks>
public sealed class MenuEmpty : PartBase<EmptyState>
{
    private MenuFilterRootContext? filter;
    private IDisposable? subscription;
    private bool ready;
    private DomTwin? domTwin;
    private TwinHandle<DomOptions>? liveRegion;
    private string? liveRegionElementId;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // MNU-EDGE-04, STR-15.
        filter = RootContext?.Filter ?? throw MenuParts.MissingFilter("Empty");
        subscription = filter.SubscribeVisibleSet(() => _ = InvokeAsync(StateHasChanged));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = ready && filter!.Engine.IsEmpty;
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        filter!.Flush();
        if (!ready)
        {
            ready = true;
            if (filter.Engine.IsEmpty)
            {
                StateHasChanged();
            }
        }

        // MNU-DOM-23: a new appearance, a new element, marks its text again; a disappearance releases the handle.
        string? elementId = Enabled ? Element?.Id : null;
        if (string.Equals(elementId, liveRegionElementId, StringComparison.Ordinal))
        {
            return;
        }

        liveRegionElementId = elementId;
        if (liveRegion is not null)
        {
            await liveRegion.DisposeAsync();
            liveRegion = null;
        }

        if (elementId is not null && Element is { } element)
        {
            await PopupPortalContext.WhenMoved(Portal);
            domTwin ??= new DomTwin(JSRuntime);
            liveRegion = await domTwin.AttachAsync(element, new DomOptions { LiveRegion = true });
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscription?.Dispose();
            if (domTwin is not null)
            {
                _ = domTwin.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    // MNU-DOM-23, ARIA-16.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "status");
        attributes.Set("aria-live", "polite");
        attributes.Set("aria-atomic", "true");
    }
}
