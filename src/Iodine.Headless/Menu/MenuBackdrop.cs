using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.4 state object (menu/backdrop/MenuBackdrop.tsx:40-43). TransitionStatus is mirrored (ADR 04 §2).
/// <summary>Represents the state of a menu backdrop, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the menu is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the menu's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record MenuBackdropState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus);

// menu/backdrop/MenuBackdropDataAttributes.ts (MNU-DOM-10).
/// <summary>Provides the names of the data attributes a menu backdrop renders.</summary>
public static class MenuBackdropDataAttributes
{
    /// <summary>Represents the attribute present while the menu is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the menu is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the backdrop begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the backdrop is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

// Base UI's Menu.Backdrop (menu/backdrop/MenuBackdrop.tsx); the contract is docs/components/menu.md §3.4 (MNU-DOM-10,
// ANI-11). A thin shell over MenuBackdropBehavior.
/// <summary>
/// Represents an overlay displayed beneath the menu popup, Base UI's <c>Menu.Backdrop</c>, rendered as a <c>div</c>.
/// </summary>
public sealed class MenuBackdrop : PartBase<MenuBackdropState>
{
    private static readonly StateAttributesMapping<MenuBackdropState> s_mapping = PopupStateMapping.CreatePopup<MenuBackdropState>();

    private MenuBackdropBehavior? behavior;
    private MenuBackdropState state = new(false, null);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private IMenuParentContext? ParentContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override MenuBackdropState State
    {
        get
        {
            bool open = behavior!.Open;
            TransitionStatus? status = behavior.TransitionStatus;
            if (state.Open != open || state.TransitionStatus != status)
            {
                state = new MenuBackdropState(open, status);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<MenuBackdropState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuBackdropBehavior("Menu", JSRuntime, StateHasChanged, InvokeAsync);
        behavior.Subscribe(RootContext, ParentContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Element);

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
