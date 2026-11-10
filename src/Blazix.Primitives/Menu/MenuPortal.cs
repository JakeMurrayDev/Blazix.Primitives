using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Menu.Portal (menu/portal/MenuPortal.tsx); the contract is docs/components/menu.md §3.3 (MNU-STR-04, STR-18,
// API-24/25, DOM-26, ARIA-18). A thin shell over MenuPortalBehavior, the PopoverPortal shape: the node renders while
// mounted or KeepMounted, display: none until the `portal` module moves it.
/// <summary>
/// Moves the menu's positioner and popup to another part of the page, Base UI's <c>Menu.Portal</c>; it renders a
/// <c>div</c> node there.
/// </summary>
public sealed class MenuPortal : PartBase<EmptyState>
{
    private MenuPortalBehavior? behavior;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? EnclosingPortal { get; set; }

    // MNU-API-24
    /// <summary>Gets or sets a value indicating whether the menu stays mounted while closed.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool KeepMounted { get; set; }

    // MNU-API-25
    /// <summary>Gets or sets the element the portal moves its node into.</summary>
    /// <value>An element, a selector or a pending container; <see langword="null"/>, the default, for the document's body.</value>
    [Parameter]
    public PortalContainerValue? Container { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override string? HostWrapperTagName => "div";

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuPortalBehavior("Menu", JSRuntime, StateHasChanged);
        behavior.Subscribe(RootContext, StateHasChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => ChildContent = behavior!.Apply(Id, KeepMounted, Container, EnclosingPortal, ChildContent);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = behavior!.RendersNode;
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Enabled ? Element : null);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && behavior is not null)
        {
            _ = behavior.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes);

    /// <inheritdoc />
    protected override void AddHostWrapperAttributes(AttributeList attributes) => behavior!.AddWrapperAttributes(attributes);

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => behavior!.BuildBeforeElement(builder);

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => behavior!.BuildAfterElement(builder);
}
