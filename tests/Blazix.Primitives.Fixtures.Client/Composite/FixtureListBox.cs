using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>
/// The test-only list element at L4 (M5 track 1, issue 159, Deliverable 12): a <c>div</c> with the list's role
/// (<c>menu</c> or <c>listbox</c>, as Base UI's <c>Menu.List</c>, <c>Select.List</c> and <c>Combobox.List</c> carry it) and
/// <c>tabindex="-1"</c>, composing a composite root in list mode in the <see cref="CompositeListRole.Floating"/> role, which
/// cascades its context to the items; the counterpart of the L2 project's <c>TestListHandle</c>. Inside a portal its handle
/// attaches after the portal's move, as every portaled part's handle does.
/// </summary>
public sealed class FixtureListBox : PartBase<EmptyState>
{
    private readonly EventCallback onKeyDown;
    private CompositeRootBehavior? behavior;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    public FixtureListBox()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ListNavigationContext? ListContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <summary>Gets or sets the list element's role.</summary>
    [Parameter]
    public string Role { get; set; } = "menu";

    /// <summary>
    /// Gets or sets a value indicating whether the module reports the items' order (the measurement page's registry
    /// figure, M5 track 1, Deliverable 14).
    /// </summary>
    [Parameter]
    public bool ReportItems { get; set; }

    /// <summary>Gets or sets the callback each order report runs, with the count of ordered items.</summary>
    [Parameter]
    public EventCallback<int> OnItemsChange { get; set; }

    /// <summary>Gets a task that completes when the floating handle is attached.</summary>
    public Task TwinAttached => behavior!.TwinAttached;

    protected override string DefaultTagName => "div";

    protected override EmptyState State => EmptyState.Instance;

    protected override bool UsesGeneratedId => true;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    protected override void OnInitialized()
    {
        behavior = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown);
        behavior.ItemsChanged += () => _ = OnItemsChange.InvokeAsync(behavior.OrderedItems.Count);
        base.OnInitialized();
    }

    protected override void OnParametersSet()
    {
        behavior!.SetList(ListContext, CompositeListRole.Floating);
        behavior.ReportItems = ReportItems;
        behavior.SetConsumerAttributes(AdditionalAttributes);
        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = behavior.Context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    // A portaled part's handle attaches once the portal has moved its node (design point 3 of M4 track 1, settled by the
    // orchestrator 2026-10-27): in place the node is `display: none`, where every item is invisible, so the open's initial
    // highlight would skip them all.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Enabled && !behavior!.TwinAttached.IsCompleted)
        {
            await PopupPortalContext.WhenMoved(Portal);
        }

        await behavior!.AfterRenderAsync(Enabled, Element, Id);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", Role);
        attributes.Set("tabindex", "-1");
        behavior!.AddAttributes(attributes, Id);
    }

    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);
}
