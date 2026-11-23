using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.4 state object (menu/backdrop/MenuBackdrop.tsx:40-43). TransitionStatus is mirrored (ADR 04 §2).
/// <summary>Represents the state of a context menu backdrop, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the menu is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the menu's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record ContextMenuBackdropState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus);

// menu/backdrop/MenuBackdropDataAttributes.ts (MNU-DOM-10).
/// <summary>Provides the names of the data attributes a context menu backdrop renders.</summary>
public static class ContextMenuBackdropDataAttributes
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

// Base UI's ContextMenu.Backdrop is Menu.Backdrop (context-menu/index.parts.ts:4-19; CTX-STR-01, STR-02: the MNU rows below are
// adopted). Decision 12 of the M5 plan and ADR 08 Amendments 2026-11-03 (3) (chosen by the user 2026-11-03): a sealed shell
// over Menu's behaviour with the family name `ContextMenu`, which its diagnostics name (CTX-EDGE-02); M5 track 7.
// Base UI's Menu.Backdrop (menu/backdrop/MenuBackdrop.tsx); the contract is docs/components/menu.md §3.4 (MNU-DOM-10,
// ANI-11). A thin shell over MenuBackdropBehavior.
/// <summary>
/// Represents an overlay displayed beneath the menu popup, Base UI's <c>ContextMenu.Backdrop</c>, rendered as a <c>div</c>.
/// </summary>
public sealed class ContextMenuBackdrop : PartBase<ContextMenuBackdropState>
{
    private static readonly StateAttributesMapping<ContextMenuBackdropState> s_mapping = PopupStateMapping.CreatePopup<ContextMenuBackdropState>();

    private MenuBackdropBehavior? behavior;
    private ContextMenuBackdropState state = new(false, null);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private IMenuParentContext? ParentContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ContextMenuBackdropState State
    {
        get
        {
            bool open = behavior!.Open;
            TransitionStatus? status = behavior.TransitionStatus;
            if (state.Open != open || state.TransitionStatus != status)
            {
                state = new ContextMenuBackdropState(open, status);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ContextMenuBackdropState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuBackdropBehavior("ContextMenu", JSRuntime, StateHasChanged, InvokeAsync);
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
