using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.11 state object (menu/checkbox-item/MenuCheckboxItem.tsx:122-126). Highlighted is mirrored (ADR 04 Amendments
// 2026-11-03 (2)).
/// <summary>Represents the state of a context menu checkbox item, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the item ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Highlighted">
/// A value indicating whether the item is highlighted; its <c>data-highlighted</c> attribute is written by the
/// browser-side behavior.
/// </param>
/// <param name="Checked">A value indicating whether the item is checked; projected as <c>data-checked</c> or <c>data-unchecked</c>.</param>
[PartState]
public sealed partial record ContextMenuCheckboxItemState(bool Disabled, [property: MirroredField] bool Highlighted, bool Checked);

// menu/checkbox-item/MenuCheckboxItemDataAttributes.ts (MNU-DOM-16).
/// <summary>Provides the names of the data attributes a context menu checkbox item renders.</summary>
public static class ContextMenuCheckboxItemDataAttributes
{
    /// <summary>Represents the attribute present while the item is checked: <c>data-checked</c>.</summary>
    public const string Checked = "data-checked";

    /// <summary>Represents the attribute present while the item is not checked: <c>data-unchecked</c>.</summary>
    public const string Unchecked = "data-unchecked";

    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the item is highlighted: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";
}

// Base UI's ContextMenu.CheckboxItem is Menu.CheckboxItem (context-menu/index.parts.ts:4-19; CTX-STR-01, STR-02: the MNU rows below are
// adopted). Decision 12 of the M5 plan and ADR 08 Amendments 2026-11-03 (3) (chosen by the user 2026-11-03): a sealed shell
// over Menu's behaviour with the family name `ContextMenu`, which its diagnostics name (CTX-EDGE-02); M5 track 7.
// Base UI's Menu.CheckboxItem (menu/checkbox-item/MenuCheckboxItem.tsx); the contract is docs/components/menu.md §3.11
// (MNU-API-41/42, DOM-16, ARIA-08, EVT-08). A thin shell over MenuItemBehavior and MenuCheckboxItemBehavior; the checked
// state is held above the element (API-41).
/// <summary>Represents a context menu item that toggles a setting on or off, Base UI's <c>ContextMenu.CheckboxItem</c>, rendered as a <c>div</c>.</summary>
public sealed class ContextMenuCheckboxItem : PartBase<ContextMenuCheckboxItemState>
{
    private static readonly StateAttributesMapping<ContextMenuCheckboxItemState> s_mapping = MenuItemStateMapping.Create<ContextMenuCheckboxItemState>();

    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly MenuCheckboxItemBehavior checkable;
    private MenuItemBehavior? behavior;
    private ContextMenuCheckboxItemState state = new(false, false, false);

    /// <summary>Initializes a new instance of the <see cref="ContextMenuCheckboxItem"/> class.</summary>
    public ContextMenuCheckboxItem()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
        checkable = new MenuCheckboxItemBehavior("ContextMenu", StateHasChanged);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ContextMenuCheckboxItem> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? ListRoot { get; set; }

    // MNU-API-41
    /// <summary>Gets or sets a value indicating whether the item is checked.</summary>
    /// <value>
    /// The checked state the consumer owns, when supplied at the first render; then an activation only reports through
    /// <see cref="OnCheckedChange"/> and <see cref="CheckedChanged"/>. Use <c>@bind-Checked</c> to follow them.
    /// </value>
    [Parameter]
    public bool Checked { get; set; }

    /// <summary>Gets or sets the callback that receives the requested checked state, for <c>@bind-Checked</c>.</summary>
    /// <value>Invoked after <see cref="OnCheckedChange"/> when the change is not canceled.</value>
    [Parameter]
    public EventCallback<bool> CheckedChanged { get; set; }

    /// <summary>Gets or sets a value indicating whether the item is checked at its first render, while <see cref="Checked"/> is not supplied.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool DefaultChecked { get; set; }

    // MNU-API-42
    /// <summary>Gets or sets the callback invoked before the checked state changes.</summary>
    /// <value>Receives the requested state and its details; calling <c>Details.Cancel()</c> keeps the current state.</value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnCheckedChange { get; set; }

    /// <summary>Gets or sets a value indicating whether a click on the item closes the menu.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool CloseOnClick { get; set; }

    /// <summary>Gets or sets a value indicating whether the item ignores user interaction.</summary>
    /// <value><see langword="false"/> by default; a disabled item stays focusable.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Gets or sets the text the typeahead matches the item by.</summary>
    /// <value>The label, or <see langword="null"/>, the default, for the item's text content.</value>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value><see langword="false"/> by default; set it when <see cref="PartBase{TState}.Render"/> supplies a <c>button</c>.</value>
    [Parameter]
    public bool NativeButton { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ContextMenuCheckboxItemState State
    {
        get
        {
            bool highlighted = behavior!.Highlighted;
            bool isChecked = checkable.Checked;
            bool disabled = behavior.Disabled;
            if (state.Disabled != disabled || state.Highlighted != highlighted || state.Checked != isChecked)
            {
                state = new ContextMenuCheckboxItemState(disabled, highlighted, isChecked);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ContextMenuCheckboxItemState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        checkable.SetParameters(parameters, Logger);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuItemBehavior("ContextMenu", MenuItemKind.CheckboxItem, JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onClick, onKeyDown, onKeyUp);
        behavior.Activated = checkable.ToggleAsync;
        behavior.Subscribe(RootContext, ListRoot);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Apply(Id, new MenuItemParameters(Disabled, Label, NativeButton, CloseOnClick), AdditionalAttributes);
        checkable.Apply(CheckedChanged, OnCheckedChange, behavior.Disabled, behavior.Highlighted);
        ChildContent = checkable.CascadeContent(ChildContent);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Enabled, Element, Id);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes, Id);
        attributes.Set("role", "menuitemcheckbox");
        attributes.Set("aria-checked", checkable.Checked ? "true" : "false");
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);
}
