using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MBR-STA-01 (menubar/Menubar.tsx:51-55). ADR 08 §3: sealed, partial and [PartState]; HasSubmenuOpen is mapped to
// data-has-submenu-open (MBR-DOM-03), which the generator would lowercase.
/// <summary>Represents the state of a menubar, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Orientation">The axis of the arrow keys that move focus between the bar's triggers; projected as <c>data-orientation</c>.</param>
/// <param name="Modal">A value indicating whether the bar's menus are modal; projected as <c>data-modal</c>.</param>
/// <param name="HasSubmenuOpen">A value indicating whether one of the bar's menus is open; projected as <c>data-has-submenu-open</c>.</param>
[PartState]
public sealed partial record MenubarState(Orientation Orientation, bool Modal, bool HasSubmenuOpen);

// MBR §3.1; menubar/MenubarDataAttributes.ts:1-13, in its order.
/// <summary>Provides the names of the data attributes a menubar renders.</summary>
public static class MenubarDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the bar's menus are modal: <c>data-modal</c>.</summary>
    public const string Modal = "data-modal";

    /// <summary>Represents the attribute that holds the bar's orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";

    /// <summary>Represents the attribute present, with an empty value, while one of the bar's menus is open: <c>data-has-submenu-open</c>.</summary>
    public const string HasSubmenuOpen = "data-has-submenu-open";
}

// Base UI's Menubar (menubar/Menubar.tsx); the contract is docs/components/menubar.md (M5 track 8, issue 195).
// - One `div` with role="menubar", the resolved id and aria-orientation for both values, before the consumer's attributes
//   (MBR-STR-01, API-05, API-06, ARIA-01, ARIA-02, DOM-04).
// - The composite root of every descendant MenuTrigger (CompositeRootBehavior): Orientation, LoopFocus, Home and End on,
//   the highlight on hover while a menu is open (MBR-PTR-06), the default propagation stop.
// - It cascades its MenubarContext as Menu's parent context, fixed with a subscription, then the composite registry.
// - The consumer's onkeydown is declined and run inside the composite's gate, as ToolbarRoot does.
/// <summary>
/// Represents a menu bar: Base UI's <c>Menubar</c>, rendered as a <c>div</c> with <c>role="menubar"</c>, around the
/// <see cref="MenuRoot{TPayload}"/> menus whose <see cref="MenuTrigger{TPayload}"/> triggers it holds. The bar is one tab
/// stop, the arrow keys of its orientation move between the triggers, and while one menu is open, hovering or focusing
/// another trigger opens its menu.
/// </summary>
public sealed class Menubar : PartBase<MenubarState>
{
    private const string DefaultTag = "div";

    private static readonly StateDataAttribute[] s_hasSubmenuOpen = [new(MenubarDataAttributes.HasSubmenuOpen, "")];

    private static readonly StateAttributesMapping<MenubarState> s_mapping =
        new StateAttributesMapping<MenubarState>().Map<bool>(nameof(MenubarState.HasSubmenuOpen), static value => value ? s_hasSubmenuOpen : null);

    private readonly EventCallback onKeyDown;
    private CompositeRootBehavior? composite;
    private MenubarContext? context;
    private MenubarState? state;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;

    /// <summary>Initializes a new instance of the <see cref="Menubar"/> class.</summary>
    public Menubar()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => composite!.HandleKeyDownAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    [CascadingParameter]
    private IMenuParentContext? EnclosingParent { get; set; }

    // MBR-API-01
    /// <summary>Gets or sets the axis of the arrow keys that move focus between the bar's triggers.</summary>
    /// <value>
    /// <see cref="Blazix.Primitives.Orientation.Horizontal"/>, the default, for the left and right arrows, the menus opening
    /// below the bar; <see cref="Blazix.Primitives.Orientation.Vertical"/> for the up and down arrows, the menus opening
    /// beside it. Reflected in <c>aria-orientation</c> and <c>data-orientation</c>.
    /// </value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    // MBR-API-02
    /// <summary>Gets or sets a value indicating whether the arrow keys wrap from the last trigger to the first and back.</summary>
    /// <value><see langword="true"/>, the default, to wrap; <see langword="false"/> to stop at either end.</value>
    [Parameter]
    public bool LoopFocus { get; set; } = true;

    // MBR-API-03
    /// <summary>Gets or sets a value indicating whether the bar's menus are modal: an open menu blocks the page behind it and locks its scrolling.</summary>
    /// <value><see langword="true"/>, the default, for modal menus; <see langword="false"/> otherwise. Reflected in <c>data-modal</c>.</value>
    [Parameter]
    public bool Modal { get; set; } = true;

    // MBR-API-04
    /// <summary>Gets or sets a value indicating whether every menu of the bar ignores user interaction.</summary>
    /// <value><see langword="true"/> to disable every menu of the bar; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override MenubarState State => state!;

    // MBR-API-05: the id is the consumer's or generated, and rendered.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override StateAttributesMapping<MenubarState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        composite = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown)
        {
            EnableHomeAndEndKeys = true,
        };
        context = new MenubarContext(OnFlagChanged, DispatchExceptionAsync);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        composite!.Orientation = CompositeLiterals.ToLiteral(Orientation);
        composite.LoopFocus = LoopFocus;
        composite.HighlightItemOnHover = context!.HasSubmenuOpen;
        composite.SetDirectionContext(DirectionContext);
        composite.SetConsumerAttributes(AdditionalAttributes);
        context.Enclosing = EnclosingParent;
        context.SetValues(Modal, Disabled, Orientation, Id);
        UpdateState();

        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            RenderFragment items = composite.Context.Cascade(cascadedContent);
            cascade = builder => RenderContextCascade(builder, items);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        context!.SetElement(Element);
        return composite!.AfterRenderAsync(Enabled, Element, Id);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            composite?.Dispose();
        }

        base.Dispose(disposing);
    }

    // MBR-ARIA-01, ARIA-02: role="menubar" and aria-orientation for both values; the consumer's win at the merge.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        composite!.AddAttributes(attributes, Id);
        attributes.Set("role", "menubar");
        attributes.Set("aria-orientation", CompositeLiterals.ToLiteral(Orientation));
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => composite!.ForwardsConsumerHandler(eventName);

    // MBR-STA-02..04, PTR-06: the flag re-renders the bar and turns the highlight on hover with it.
    private void OnFlagChanged()
    {
        composite!.HighlightItemOnHover = context!.HasSubmenuOpen;
        UpdateState();
        StateHasChanged();
    }

    private void RenderContextCascade(RenderTreeBuilder builder, RenderFragment items)
    {
        builder.OpenComponent<CascadingValue<IMenuParentContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<IMenuParentContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<IMenuParentContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<IMenuParentContext>.ChildContent), items);
        builder.CloseComponent();
    }

    // A new record only when the state changed (ADR 16 rule 7).
    private void UpdateState()
    {
        bool flag = context!.HasSubmenuOpen;
        if (state is { } current && current.Orientation == Orientation && current.Modal == Modal && current.HasSubmenuOpen == flag)
        {
            return;
        }

        state = new MenubarState(Orientation, Modal, flag);
    }
}
