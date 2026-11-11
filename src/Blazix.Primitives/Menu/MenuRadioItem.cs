using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.14 state object (menu/radio-item/MenuRadioItem.tsx). Highlighted is mirrored (ADR 04 Amendments 2026-11-03 (2)).
/// <summary>Represents the state of a menu radio item, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the item ignores user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Highlighted">
/// A value indicating whether the item is highlighted; its <c>data-highlighted</c> attribute is written by the
/// browser-side behavior.
/// </param>
/// <param name="Checked">A value indicating whether the item is selected; projected as <c>data-checked</c> or <c>data-unchecked</c>.</param>
[PartState]
public sealed partial record MenuRadioItemState(bool Disabled, [property: MirroredField] bool Highlighted, bool Checked);

// menu/radio-item/MenuRadioItemDataAttributes.ts (MNU-DOM-16).
/// <summary>Provides the names of the data attributes a menu radio item renders.</summary>
public static class MenuRadioItemDataAttributes
{
    /// <summary>Represents the attribute present while the item is selected: <c>data-checked</c>.</summary>
    public const string Checked = "data-checked";

    /// <summary>Represents the attribute present while the item is not selected: <c>data-unchecked</c>.</summary>
    public const string Unchecked = "data-unchecked";

    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the item is highlighted: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";
}

// Base UI's Menu.RadioItem (menu/radio-item/MenuRadioItem.tsx); the contract is docs/components/menu.md §3.14 (MNU-API-47,
// DOM-16, ARIA-09, EVT-09). A thin shell over MenuItemBehavior and MenuRadioItemBehavior.
/// <summary>Represents a menu item that works like a radio button, Base UI's <c>Menu.RadioItem</c>, rendered as a <c>div</c>.</summary>
/// <typeparam name="TValue">The type of the item's value, the group's.</typeparam>
public sealed class MenuRadioItem<TValue> : PartBase<MenuRadioItemState>
{
    private static readonly StateAttributesMapping<MenuRadioItemState> s_mapping = MenuItemStateMapping.Create<MenuRadioItemState>();

    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly MenuRadioItemBehavior<TValue> radio;
    private MenuItemBehavior? behavior;
    private MenuRadioItemState state = new(false, false, false);

    /// <summary>Initializes a new instance of the <see cref="MenuRadioItem{TValue}"/> class.</summary>
    public MenuRadioItem()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
        radio = new MenuRadioItemBehavior<TValue>("Menu", StateHasChanged);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<MenuRadioItem<TValue>> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? ListRoot { get; set; }

    [CascadingParameter]
    private MenuRadioGroupContext<TValue>? Group { get; set; }

    // MNU-API-47
    /// <summary>Gets or sets the value the group takes when the item is selected.</summary>
    /// <value>The item's value, compared with the group's by strict equality.</value>
    [Parameter]
    public TValue Value { get; set; } = default!;

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
    protected override MenuRadioItemState State
    {
        get
        {
            bool disabled = behavior!.Disabled || radio.GroupDisabled;
            bool highlighted = behavior!.Highlighted;
            bool isChecked = radio.Checked;
            if (state.Disabled != disabled || state.Highlighted != highlighted || state.Checked != isChecked)
            {
                state = new MenuRadioItemState(disabled, highlighted, isChecked);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<MenuRadioItemState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The item is outside a menu or a radio group.</exception>
    protected override void OnInitialized()
    {
        behavior = new MenuItemBehavior("Menu", MenuItemKind.RadioItem, JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onClick, onKeyDown, onKeyUp);
        behavior.Activated = radio.SelectAsync;
        behavior.Subscribe(RootContext, ListRoot);
        radio.Subscribe(Group);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        bool disabled = Disabled || radio.GroupDisabled;
        behavior!.Apply(Id, new MenuItemParameters(disabled, Label, NativeButton, CloseOnClick), AdditionalAttributes);
        radio.Apply(Value, disabled, behavior.Highlighted);
        ChildContent = radio.CascadeContent(ChildContent);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        radio.Apply(Value, behavior!.Disabled || radio.GroupDisabled, behavior.Highlighted);
        // MNU-STA-15 (M5 track 12): an item the menu's filter hides renders nothing.
        Enabled = !behavior.FilteredOut;
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Enabled, Element, Id);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
            radio.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes, Id);
        attributes.Set("role", "menuitemradio");
        attributes.Set("aria-checked", radio.Checked ? "true" : "false");
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);
}
