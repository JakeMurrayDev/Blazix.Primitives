using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.15 state object (menu/radio-item-indicator/MenuRadioItemIndicator.tsx). TransitionStatus is the indicator's
// own transitions handle's (on `checked`, MNU-ANI-13): mirrored.
/// <summary>Represents the state of a context menu radio item indicator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Checked">A value indicating whether the item is checked; projected as <c>data-checked</c> or <c>data-unchecked</c>.</param>
/// <param name="Disabled">A value indicating whether the item is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Highlighted">A value indicating whether the item is highlighted; projected as <c>data-highlighted</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the indicator's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record ContextMenuRadioItemIndicatorState(bool Checked, bool Disabled, bool Highlighted, [property: MirroredField] TransitionStatus? TransitionStatus);

// menu/radio-item-indicator/MenuRadioItemIndicatorDataAttributes.ts (MNU-DOM-17).
/// <summary>Provides the names of the data attributes a context menu radio item indicator renders.</summary>
public static class ContextMenuRadioItemIndicatorDataAttributes
{
    /// <summary>Represents the attribute present while the item is checked: <c>data-checked</c>.</summary>
    public const string Checked = "data-checked";

    /// <summary>Represents the attribute present while the item is not checked: <c>data-unchecked</c>.</summary>
    public const string Unchecked = "data-unchecked";

    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the item is highlighted: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";

    /// <summary>Represents the attribute present while the indicator begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the indicator is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

// Base UI's ContextMenu.RadioItemIndicator is Menu.RadioItemIndicator (context-menu/index.parts.ts:4-19; CTX-STR-01, STR-02: the MNU rows below are
// adopted). Decision 12 of the M5 plan and ADR 08 Amendments 2026-11-03 (3) (chosen by the user 2026-11-03): a sealed shell
// over Menu's behaviour with the family name `ContextMenu`, which its diagnostics name (CTX-EDGE-02); M5 track 7.
// Base UI's Menu.RadioItemIndicator (menu/radio-item-indicator/MenuRadioItemIndicator.tsx); the contract is
// docs/components/menu.md §3.15 (MNU-API-43, DOM-17, ANI-13). A thin shell over MenuItemIndicatorBehavior.
/// <summary>
/// Indicates whether the menu radio item is checked, Base UI's <c>ContextMenu.RadioItemIndicator</c>, rendered as a <c>span</c>.
/// </summary>
public sealed class ContextMenuRadioItemIndicator : PartBase<ContextMenuRadioItemIndicatorState>
{
    private static readonly StateAttributesMapping<ContextMenuRadioItemIndicatorState> s_mapping = MenuItemStateMapping.Create<ContextMenuRadioItemIndicatorState>();

    private MenuItemIndicatorBehavior? behavior;
    private ContextMenuRadioItemIndicatorState state = new(false, false, false, null);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private MenuCheckableItemContext? Item { get; set; }

    /// <summary>Gets or sets a value indicating whether the indicator stays mounted while the item is not checked.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool KeepMounted { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "span";

    /// <inheritdoc />
    protected override ContextMenuRadioItemIndicatorState State
    {
        get
        {
            MenuCheckableItemContext item = behavior!.Item;
            TransitionStatus? status = behavior.TransitionStatus;
            if (state.Checked != item.Checked || state.Disabled != item.Disabled || state.Highlighted != item.Highlighted || state.TransitionStatus != status)
            {
                state = new ContextMenuRadioItemIndicatorState(item.Checked, item.Disabled, item.Highlighted, status);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ContextMenuRadioItemIndicatorState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuItemIndicatorBehavior("ContextMenu", JSRuntime, StateHasChanged, InvokeAsync);
        behavior.Subscribe(Item, radio: true);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(KeepMounted);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = behavior!.Rendered;
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
}
