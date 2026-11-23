using Blazix.Primitives.Rendering;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Select.ItemText (select/item-text/SelectItemText.tsx); SEL-DOM-38, SEL-API-42: the item's text element, the
// typeahead label's source (its id in the item's composite registration, CMP-STA-06) and the alignment reference of
// aligned placement (its id in the store, SEL-POS-07). It renders a generated id for both.
/// <summary>Represents the text of a select item, Base UI's <c>Select.ItemText</c>, rendered as a <c>div</c>.</summary>
public sealed class SelectItemText : PartBase<EmptyState>
{
    private SelectItemContext? item;

    [CascadingParameter]
    private SelectStore? RootStore { get; set; }

    [CascadingParameter]
    private SelectItemContext? ItemContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The text is not inside a <see cref="SelectRoot{TValue}"/> and a <see cref="SelectItem{TValue}"/>.</exception>
    protected override void OnInitialized()
    {
        SelectStore.Require(RootStore);
        item = SelectItemContext.Require(ItemContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => item!.SetText(Enabled ? Id : null);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            item?.SetText(null);
        }

        base.Dispose(disposing);
    }
}

// SEL-STA-24 (utils/ItemIndicator.tsx:22-26): track 4's ItemIndicatorBehavior's shell record (TestItemIndicatorState's
// shape); TransitionStatus is the `transitions` module's (ADR 04 §2): mirrored.
/// <summary>Represents the state of a select item indicator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Selected">A value indicating whether the item's value is selected; projected as <c>data-selected</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the indicator's transition, or <see langword="null"/>; its <c>data-starting-style</c> and
/// <c>data-ending-style</c> are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record SelectItemIndicatorState(bool Selected, [property: MirroredField] TransitionStatus? TransitionStatus);

// select/item-indicator/SelectItemIndicatorDataAttributes.ts (SEL-DOM-39).
/// <summary>Provides the names of the data attributes a select item indicator renders.</summary>
public static class SelectItemIndicatorDataAttributes
{
    /// <summary>Represents the attribute present while the item's value is selected: <c>data-selected</c>.</summary>
    public const string Selected = "data-selected";

    /// <summary>Represents the attribute present while the indicator is animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the indicator is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

// Base UI's Select.ItemIndicator (select/item-indicator/SelectItemIndicator.tsx over utils/ItemIndicator.tsx); SEL-STR-11,
// SEL-API-45, SEL-DOM-39, SEL-ANI-12, RND-STR-10: track 4's ItemIndicatorBehavior composed as TestItemIndicator, the
// selection read from the item's context, which renders it only when the item's Selected changed.
/// <summary>
/// Represents the mark of a selected select item, Base UI's <c>Select.ItemIndicator</c>, rendered as a <c>span</c> while
/// the item is selected.
/// </summary>
/// <remarks>Without content it shows ✔️.</remarks>
public sealed class SelectItemIndicator : PartBase<SelectItemIndicatorState>
{
    private readonly Action selectionChanged;
    private SelectItemContext? item;
    private ItemIndicatorBehavior? behavior;
    private SelectItemIndicatorState? state;

    /// <summary>Initializes a new instance of the <see cref="SelectItemIndicator"/> class.</summary>
    public SelectItemIndicator()
    {
        selectionChanged = OnSelectionChanged;
    }

    private bool Shown => KeepMounted || behavior!.Selected;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private SelectItemContext? ItemContext { get; set; }

    // SEL-API-45
    /// <summary>Gets or sets a value indicating whether the indicator stays in the DOM while the item is not selected.</summary>
    /// <value><see langword="false"/> by default; when <see langword="true"/>, its exit transition can run.</value>
    [Parameter]
    public bool KeepMounted { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "span";

    /// <inheritdoc />
    protected override SelectItemIndicatorState State => state ??= new SelectItemIndicatorState(behavior!.Selected, behavior.TransitionStatus);

    /// <summary>Gets a task that completes when the indicator's behavior is attached.</summary>
    internal Task TwinAttached => behavior!.TwinAttached;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The indicator is not inside a <see cref="SelectItem{TValue}"/>.</exception>
    protected override void OnInitialized()
    {
        item = SelectItemContext.Require(ItemContext);
        behavior = new ItemIndicatorBehavior(JSRuntime, action => InvokeAsync(action), () =>
        {
            state = null;
            StateHasChanged();
        });
        behavior.Initialize(item.Selected, KeepMounted);
        item.AddListener(selectionChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Update(item!.Selected, KeepMounted);
        state = null;
        ChildContent ??= ItemIndicatorBehavior.DefaultContent;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // SEL-STR-11 (SelectItemIndicator.tsx:18-23): an indicator that is not kept mounted renders only while selected,
        // with no exit; the shared behavior's exit holds for a kept-mounted one.
        if (!Shown)
        {
            ((IPartHost)this).SetElement(null);
            return;
        }

        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Shown ? Element : null);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            item?.RemoveListener(selectionChanged);
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes);

    private void OnSelectionChanged()
    {
        if (behavior!.Update(item!.Selected, KeepMounted))
        {
            state = null;
            StateHasChanged();
        }
    }
}
