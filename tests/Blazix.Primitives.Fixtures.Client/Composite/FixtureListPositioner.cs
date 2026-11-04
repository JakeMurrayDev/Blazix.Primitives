using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>
/// The test-only list positioner at L4 (M5 track 1, issue 159, Deliverable 12): a <c>div</c> around the popup, positioned
/// above the internal backdrop (<c>position: relative; z-index: 1</c>, as Base UI's demos place theirs), carrying the
/// <c>interactions</c> floating handle, as Popover's Positioner does, so the session knows the floating element and a press
/// inside it is no outside press. No anchoring: no suite title reads the popup's position.
/// </summary>
public sealed class FixtureListPositioner : PartBase<EmptyState>
{
    private static readonly InteractionsFloatingOptions s_options = new();

    private InteractionsFloatingBehavior? floating;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private FixtureListContext? HostContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    protected override string DefaultTagName => "div";

    protected override EmptyState State => EmptyState.Instance;

    protected override bool UsesGeneratedId => true;

    protected override void OnInitialized()
    {
        floating = new InteractionsFloatingBehavior(JSRuntime, request => HostContext!.RequestAsync(request), DispatchExceptionAsync);
        base.OnInitialized();
    }

    protected override void OnParametersSet()
    {
        floating!.PortalContext = Portal;
        floating.SessionToken = HostContext!.Store.SessionToken;
        floating.Options = s_options;
    }

    protected override Task OnAfterRenderAsync(bool firstRender) => floating!.AfterRenderAsync(Enabled, Element, Id);

    protected override void Dispose(bool disposing)
    {
        if (disposing && floating is not null)
        {
            _ = floating.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set("style", "position: relative; z-index: 1");
}
