using Blazix.Primitives.Fixtures.Client.PopupFixtures;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>
/// The test-only popup list root at L4 (M5 track 1, issue 159, Deliverable 12; plan §Settled "Test-only hosts"), the
/// counterpart of the L2 project's <c>TestListRoot</c>: it renders the M4 popup host's <see cref="FixturePopupRoot{TPayload}"/>
/// with the outside press on, and inside it the list's scope, which composes <see cref="ListNavigationBehavior"/> over the
/// popup store's session and request path and cascades the list. Every list option is a parameter; the selectable value
/// is test-only (a label, <c>aria-selected</c> on the items, <see cref="OnValueChange"/> recorded by the page).
/// </summary>
/// <remarks>
/// The list's handles attach after the session's handle (<see cref="ListNavigationBehavior.SessionReady"/>): the root
/// completes that task in its first after-render, once the popup root's session reports its attach, so the order does not
/// depend on which component's after-render runs first. <see cref="OnAttached"/> runs once the session and the trigger's
/// handles are attached, for the page's <c>data-attached</c>.
/// </remarks>
public sealed class FixtureListRoot : ComponentBase
{
    private readonly TaskCompletionSource sessionReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly FixtureListContext context = new();
    private FixturePopupRoot<NoPayload>? popupRoot;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public bool DefaultOpen { get; set; }

    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    /// <summary>Gets or sets a value indicating whether the items carry a value (<c>aria-selected</c>, <see cref="OnValueChange"/>).</summary>
    [Parameter]
    public bool Selectable { get; set; }

    /// <summary>Gets or sets the label of the item selected initially.</summary>
    [Parameter]
    public string? DefaultValue { get; set; }

    /// <summary>Gets or sets the callback an activated item's label is reported to while <see cref="Selectable"/>.</summary>
    [Parameter]
    public EventCallback<string> OnValueChange { get; set; }

    /// <summary>Gets or sets the items' labels in order, from which the selected index is found.</summary>
    [Parameter]
    public IReadOnlyList<string> Items { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the popup is modal: the internal backdrop covers the page while open.</summary>
    [Parameter]
    public bool Modal { get; set; }

    /// <summary>Gets or sets a value indicating whether the reference keeps the focus (an input, <c>aria-activedescendant</c>).</summary>
    [Parameter]
    public bool Virtual { get; set; }

    [Parameter]
    public bool Enabled { get; set; } = true;

    [Parameter]
    public bool LoopFocus { get; set; }

    [Parameter]
    public string Orientation { get; set; } = "vertical";

    [Parameter]
    public string? TriggerOrientation { get; set; }

    [Parameter]
    public bool Rtl { get; set; }

    [Parameter]
    public bool Nested { get; set; }

    [Parameter]
    public string? ParentOrientation { get; set; }

    [Parameter]
    public bool? FocusItemOnOpen { get; set; }

    [Parameter]
    public bool AllowEscape { get; set; }

    [Parameter]
    public bool OpenOnArrowKeyDown { get; set; } = true;

    [Parameter]
    public bool FocusItemOnHover { get; set; } = true;

    [Parameter]
    public bool ResetOnPointerLeave { get; set; } = true;

    /// <summary>Gets or sets the indices navigation skips; empty (the families' value) for none, <see langword="null"/> to read the DOM.</summary>
    [Parameter]
    public IReadOnlyList<int>? DisabledIndices { get; set; } = [];

    [Parameter]
    public bool DeriveDisabledIndices { get; set; }

    [Parameter]
    public bool Grid { get; set; }

    [Parameter]
    public bool KeepOpenOnCrossOrientationKey { get; set; }

    [Parameter]
    public bool Typeahead { get; set; }

    [Parameter]
    public int TypeaheadResetMs { get; set; } = 750;

    /// <summary>Gets or sets the typeahead's skip: <c>element</c> (Menu), <c>attribute</c> (Select) or <see langword="null"/>.</summary>
    [Parameter]
    public string? TypeaheadSkip { get; set; }

    [Parameter]
    public IReadOnlyList<string?>? TypeaheadLabels { get; set; }

    /// <summary>Gets or sets the callback that runs once the session and the trigger's handles are attached.</summary>
    [Parameter]
    public EventCallback OnAttached { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<FixturePopupRoot<NoPayload>>(0);
        builder.AddComponentParameter(1, nameof(FixturePopupRoot<NoPayload>.DefaultOpen), DefaultOpen);
        builder.AddComponentParameter(2, nameof(FixturePopupRoot<NoPayload>.OnOpenChange), OnOpenChange);
        builder.AddComponentParameter(3, nameof(FixturePopupRoot<NoPayload>.OnOpenChangeComplete), OnOpenChangeComplete);
        builder.AddComponentParameter(4, nameof(FixturePopupRoot<NoPayload>.OutsidePress), true);
        builder.AddComponentParameter(5, nameof(FixturePopupRoot<NoPayload>.ChildContent), (RenderFragment)BuildScope);
        builder.AddComponentReferenceCapture(6, reference => popupRoot = (FixturePopupRoot<NoPayload>)reference);
        builder.CloseComponent();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        await popupRoot!.SessionAttached;
        sessionReady.TrySetResult();
        await context.TriggerAttached;
        await OnAttached.InvokeAsync();
    }

    private void BuildScope(RenderTreeBuilder builder)
    {
        builder.OpenComponent<FixtureListScope>(0);
        builder.AddComponentParameter(1, nameof(FixtureListScope.Root), this);
        builder.AddComponentParameter(2, nameof(FixtureListScope.Context), context);
        builder.AddComponentParameter(3, nameof(FixtureListScope.SessionReady), sessionReady.Task);
        builder.AddComponentParameter(4, nameof(FixtureListScope.ChildContent), ChildContent);
        builder.CloseComponent();
    }
}
