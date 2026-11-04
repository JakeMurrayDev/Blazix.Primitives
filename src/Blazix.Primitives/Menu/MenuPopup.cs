using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.6 state object (menu/popup/MenuPopup.tsx:137-144). TransitionStatus is mirrored (ADR 04 §2); Side and Align are
// the positioner's mirror, rendered by C# a round trip after the positioner's (MNU-DOM-06; RND-STA-01's row).
/// <summary>Represents the state of a menu popup, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the menu is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the menu's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
/// <param name="Side">The side the positioner placed the menu on, or <see langword="null"/>; projected as <c>data-side</c>.</param>
/// <param name="Align">The alignment the positioner chose, or <see langword="null"/>; projected as <c>data-align</c>.</param>
/// <param name="Nested">A value indicating whether the menu is a submenu; projected as <c>data-nested</c>.</param>
/// <param name="Instant">Why the next transition is skipped, or <see langword="null"/>; projected as <c>data-instant</c>.</param>
[PartState]
public sealed partial record MenuPopupState(
    bool Open,
    [property: MirroredField] TransitionStatus? TransitionStatus,
    Side? Side,
    Align? Align,
    bool Nested,
    MenuInstantType? Instant);

// menu/popup/MenuPopupDataAttributes.ts (MNU-DOM-06).
/// <summary>Provides the names of the data attributes a menu popup renders.</summary>
public static class MenuPopupDataAttributes
{
    /// <summary>Represents the attribute present while the menu is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the menu is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the menu begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the menu is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;

    /// <summary>Represents the attribute that names the side the menu is placed on: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute that names the menu's alignment: <c>data-align</c>.</summary>
    public const string Align = PopupDataAttributes.Align;

    /// <summary>Represents the attribute present while the menu is a submenu: <c>data-nested</c>.</summary>
    public const string Nested = "data-nested";

    /// <summary>Represents the attribute that names why the next transition is skipped: <c>data-instant</c>.</summary>
    public const string Instant = "data-instant";
}

// Base UI's Menu.Popup (menu/popup/MenuPopup.tsx); the contract is docs/components/menu.md §3.6, §5.4, §6.3. A thin shell
// over MenuPopupBehavior, which ContextMenuPopup (track 7) shares.
/// <summary>Represents a container for the menu's items, Base UI's <c>Menu.Popup</c>, rendered as a <c>div</c> with the <c>menu</c> role.</summary>
/// <remarks>Place it inside the menu's positioner. Its items are navigated with the arrow keys and found by typing.</remarks>
public sealed class MenuPopup : PartBase<MenuPopupState>
{
    private static readonly StateAttributesMapping<MenuPopupState> s_mapping = PopupStateMapping.CreatePopup<MenuPopupState>();

    private readonly EventCallback onKeyDown;
    private MenuPopupBehavior? behavior;
    private MenuPopupState? state;

    /// <summary>Initializes a new instance of the <see cref="MenuPopup"/> class.</summary>
    public MenuPopup()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<MenuPopup> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    private bool HasConsumerId => AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("id");

    private string? RenderedId => HasConsumerId ? AdditionalAttributes!["id"] as string : RootContext?.State.FloatingId;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PositionerContext? PositionerContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [CascadingParameter]
    private ToolbarRootContext? ToolbarContext { get; set; }

    // MNU-API-31
    /// <summary>Gets or sets where focus moves when the menu closes.</summary>
    /// <value>
    /// <see langword="null"/>, the default, or <see langword="true"/> for the trigger, else the element focused before the
    /// menu opened; <see langword="false"/> to leave focus where it is; an element or the id of an element to focus it.
    /// </value>
    [Parameter]
    public FocusTargetValue? FinalFocus { get; set; }

    /// <summary>Gets or sets a function that decides where focus moves when the menu closes, from how it was closed.</summary>
    /// <value>
    /// A function called once per close with the interaction type. Its result is used as <see cref="FinalFocus"/> would be,
    /// except that <see langword="null"/> leaves focus where it is. When it is set, <see cref="FinalFocus"/> is ignored.
    /// </value>
    [Parameter]
    public Func<InteractionType, FocusTargetValue?>? FinalFocusResolver { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirror here.
    /// <inheritdoc />
    protected override MenuPopupState State
    {
        get
        {
            MenuPopupBehavior current = behavior!;
            if (state is null || state.Open != current.Open || state.TransitionStatus != current.TransitionStatus || state.Side != current.Side
                || state.Align != current.Align || state.Nested != current.Nested || state.Instant != current.Instant)
            {
                state = new MenuPopupState(current.Open, current.TransitionStatus, current.Side, current.Align, current.Nested, current.Instant);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<MenuPopupState>? StateAttributesMapping => s_mapping;

    /// <summary>Gets a task that completes once the popup's handles have attached.</summary>
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

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
        behavior = new MenuPopupBehavior("Menu", JSRuntime, Logger, IdGenerator, StateHasChanged, OnMirrorChanged, InvokeAsync, DispatchExceptionAsync, onKeyDown);
        behavior.Subscribe(RootContext, PositionerContext, Portal, ToolbarContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Apply(FinalFocus, FinalFocusResolver, RenderedId);
        ChildContent = behavior.CascadeContent(ChildContent);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Element, RenderedId);

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
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes, HasConsumerId);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => behavior!.BuildBeforeElement(builder);

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => behavior!.BuildAfterElement(builder);

    // MNU-DOM-06: the Popup renders the Positioner's mirrored placement, only when its state record changed.
    private void OnMirrorChanged()
    {
        MenuPopupState? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }
}
