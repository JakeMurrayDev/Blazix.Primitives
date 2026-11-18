using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// DRW §3.6, DRW-STA-01 (drawer/swipe-area/DrawerSwipeArea.tsx:437-442): the swipe area's state object. Swiping is mirrored
// (ADR 04 §2): the `drawer` module writes the area's data-swiping during its gesture (8b).
/// <summary>Represents the state of a drawer's swipe area, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the drawer is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="Swiping">
/// A value indicating whether the area's own gesture is active; its <c>data-swiping</c> attribute is written by the
/// browser-side behavior.
/// </param>
/// <param name="SwipeDirection">The direction that opens the drawer; projected as <c>data-swipe-direction</c>.</param>
/// <param name="Disabled">A value indicating whether swiping to open is disabled; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record DrawerSwipeAreaState(bool Open, [property: MirroredField] bool Swiping, SwipeDirection SwipeDirection, bool Disabled);

// DRW-STR-02 (drawer/swipe-area/DrawerSwipeAreaDataAttributes.ts).
/// <summary>Provides the names of the data attributes a drawer's swipe area renders.</summary>
public static class DrawerSwipeAreaDataAttributes
{
    /// <summary>Represents the attribute present while the drawer is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute present while the drawer is closed: <c>data-closed</c>.</summary>
    public const string Closed = "data-closed";

    /// <summary>Represents the attribute present while the area is being swiped: <c>data-swiping</c>.</summary>
    public const string Swiping = "data-swiping";

    /// <summary>Represents the attribute that holds the direction that opens the drawer: <c>data-swipe-direction</c>.</summary>
    public const string SwipeDirection = "data-swipe-direction";

    /// <summary>Represents the attribute present while swiping to open is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Drawer.SwipeArea (drawer/swipe-area/DrawerSwipeArea.tsx:80-490); the contract is docs/components/drawer.md (§3.6,
// §5.5, §6.2.6; DRW-STR-07). Its own type (decision 20 (a) of the M6 plan, chosen by the user 2026-11-18). C# renders the
// state attributes, role and aria-hidden, the id and the inline pointer-events and touch-action, and registers the element in
// the root's trigger registry after each render whose id or element changed (Base UI's layout effect over
// useTriggerRegistration); the gesture, the open movement and the outside-press guard are the `drawer` module's (8b).
/// <summary>
/// Represents an invisible area that opens the drawer when swiped, Base UI's <c>Drawer.SwipeArea</c>, rendered as a
/// <c>div</c>.
/// </summary>
/// <remarks>
/// Place it along the edge the drawer opens from. It is hidden from assistive technology and takes no pointer events while
/// the drawer is open or the area is disabled.
/// </remarks>
public sealed class DrawerSwipeArea : PartBase<DrawerSwipeAreaState>
{
    private const string PanX = "touch-action: pan-x";
    private const string PanY = "touch-action: pan-y";
    private const string DisabledPanX = "pointer-events: none; " + PanX;
    private const string DisabledPanY = "pointer-events: none; " + PanY;

    private static readonly StateAttributesMapping<DrawerSwipeAreaState> s_mapping = PopupStateMapping.CreateOpen<DrawerSwipeAreaState>()
        .Map<SwipeDirection>(nameof(DrawerSwipeAreaState.SwipeDirection), DrawerSwipeDirections.Project);

    private IDisposable? openSubscription;
    private IDisposable? settingsSubscription;
    private string? registeredId;
    private PopupTriggerElement registeredElement;
    private DrawerSwipeAreaState state = new(false, false, Primitives.SwipeDirection.Up, false);

    private SwipeDirection OpeningDirection => SwipeDirection ?? DrawerSwipeDirections.Opposite(Drawer!.SwipeDirection);

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    [CascadingParameter]
    private DrawerRootContext? Drawer { get; set; }

    // DRW-API-08
    /// <summary>Gets or sets a value indicating whether swiping to open is disabled.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // DRW-API-09
    /// <summary>Gets or sets the direction that opens the drawer.</summary>
    /// <value>
    /// <see langword="null"/>, the default, for the opposite of the root's <see cref="DrawerRoot{TPayload}.SwipeDirection"/>:
    /// up for a drawer dismissed downward.
    /// </value>
    [Parameter]
    public SwipeDirection? SwipeDirection { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override DrawerSwipeAreaState State
    {
        get
        {
            bool open = Context!.Open;
            SwipeDirection direction = OpeningDirection;
            if (state.Open != open || state.SwipeDirection != direction || state.Disabled != Disabled)
            {
                state = new DrawerSwipeAreaState(open, false, direction, Disabled);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<DrawerSwipeAreaState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null || Drawer is null)
        {
            throw DialogParts.MissingRoot("Drawer");
        }

        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender) => Register();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            openSubscription?.Dispose();
            settingsSubscription?.Dispose();
            Unregister(disposing: true);
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        openSubscription = Context!.Subscribe(DialogFields.Open, StateHasChanged);
        settingsSubscription = Drawer!.Subscribe(DrawerFields.Settings, StateHasChanged);
    }

    // DRW-DOM-29, DRW-PTR-26: enabled iff not disabled and (closed or its own gesture active); the gesture is the module's, so
    // C# renders the resting value of an open drawer.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        bool enabled = !Disabled && !Context!.Open;
        bool horizontal = DrawerSwipeDirections.IsHorizontal(OpeningDirection);
        attributes.Set("role", "presentation");
        attributes.Set("aria-hidden", "true");
        attributes.Set("style", enabled ? (horizontal ? PanY : PanX) : (horizontal ? DisabledPanY : DisabledPanX));
    }

    // DRW-API-10: a registration is the pair (id, element); a change of either unregisters, then registers again.
    private void Register()
    {
        string? id = Id;
        PopupTriggerElement next = new(this, Element);
        if (registeredId is not null && string.Equals(registeredId, id, StringComparison.Ordinal) && registeredElement == next)
        {
            return;
        }

        Unregister(disposing: false);
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        registeredId = id;
        registeredElement = next;
        Drawer!.RegisterTrigger(id, next);
    }

    private void Unregister(bool disposing)
    {
        if (registeredId is null)
        {
            return;
        }

        string previous = registeredId;
        registeredId = null;
        Drawer!.UnregisterTrigger(previous, registeredElement, disposing);
    }
}
