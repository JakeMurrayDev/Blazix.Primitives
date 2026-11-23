using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// DRW §3.9: Drawer.Backdrop's state object (drawer/backdrop/DrawerBackdrop.tsx:30-33), DRW-STA-01.
// TransitionStatus is mirrored (ADR 04 §2).
/// <summary>Represents the state of a drawer's backdrop, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the drawer is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the drawer's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record DrawerBackdropState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus);

// DRW-STR-02 (drawer/backdrop/DrawerBackdropDataAttributes.ts): the open and transition attributes. Base UI does not export
// the data-swiping and data-swipe-dismiss the `drawer` module writes on it (DRW-DOM-07, DOM-20).
/// <summary>Provides the names of the data attributes a drawer's backdrop renders.</summary>
public static class DrawerBackdropDataAttributes
{
    /// <summary>Represents the attribute present while the drawer is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute present while the drawer is closed: <c>data-closed</c>.</summary>
    public const string Closed = "data-closed";

    /// <summary>Represents the attribute present while the drawer begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the drawer is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";
}

// DRW-STR-02 (drawer/backdrop/DrawerBackdropCssVars.ts).
/// <summary>Provides the names of the CSS variables a drawer's backdrop sets.</summary>
public static class DrawerBackdropCssVars
{
    /// <summary>Represents the progress of a swipe that dismisses the drawer, from 0 to 1: <c>--drawer-swipe-progress</c>.</summary>
    public const string SwipeProgress = "--drawer-swipe-progress";
}

// Base UI's Drawer.Backdrop (drawer/backdrop/DrawerBackdrop.tsx:17-55); the contract is docs/components/drawer.md (§3.9;
// DRW-STR-03 over dialog.md §3.4, §5.3). Its own type over DialogBackdropBehavior (decision 20 (a) of the M6 plan, chosen
// by the user 2026-11-18). Its inline style is Base UI's order (DrawerBackdrop.tsx:43-49): pointer-events: none while closed
// (DRW-DOM-17), user-select: none (DLG-DOM-07), --drawer-swipe-progress: 0 (DOM-18) and --drawer-swipe-strength: 1 (DOM-19,
// never changed: §16 Q2), then the consumer's. The C# → JS cells are C#'s until the `drawer` module takes the element (8b).
/// <summary>
/// Represents an overlay displayed beneath the drawer's popup, Base UI's <c>Drawer.Backdrop</c>, rendered as a
/// <c>div</c>.
/// </summary>
/// <remarks>
/// Pressing it does not close the drawer. A backdrop inside a nested drawer renders nothing unless
/// <see cref="ForceRender"/> is set.
/// </remarks>
public sealed class DrawerBackdrop : PartBase<DrawerBackdropState>
{
    private const string OpenStyle = "user-select: none; -webkit-user-select: none; --drawer-swipe-progress: 0; --drawer-swipe-strength: 1";
    private const string ClosedStyle = "pointer-events: none; " + OpenStyle;

    private static readonly StateAttributesMapping<DrawerBackdropState> s_mapping = PopupStateMapping.CreateOpen<DrawerBackdropState>();

    private DialogBackdropBehavior? behavior;
    private DrawerBackdropState state = new(false, null);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    [CascadingParameter]
    private DrawerRootContext? Drawer { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    // DLG-API-19
    /// <summary>Gets or sets a value indicating whether the backdrop renders even when the drawer is nested in another dialog.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool ForceRender { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override DrawerBackdropState State
    {
        get
        {
            bool open = behavior!.Open;
            TransitionStatus? status = behavior.TransitionStatus;
            if (state.Open != open || state.TransitionStatus != status)
            {
                state = new DrawerBackdropState(open, status);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<DrawerBackdropState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures: completes once the transitions handle is attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw DialogParts.MissingRoot("Drawer");
        }

        behavior = new DialogBackdropBehavior(JSRuntime, StateHasChanged, InvokeAsync);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(behavior.Renders(ForceRender), Id);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = behavior!.Renders(ForceRender);
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        // M6 track 8b: the Viewport's drawer handle writes data-swiping, data-swipe-dismiss and the progress on this element.
        Drawer?.SetBackdropElement(Enabled ? Element : null);
        return behavior!.OnAfterRenderAsync(Enabled, Element, Id, Portal);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && Element is { } element && string.Equals(Drawer?.BackdropElement?.Id, element.Id, StringComparison.Ordinal))
        {
            Drawer!.SetBackdropElement(null);
        }

        if (disposing && behavior is not null)
        {
            _ = behavior.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors() => behavior!.Subscribe(Context!);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes);
        attributes.Set("style", behavior.Open ? OpenStyle : ClosedStyle);
    }
}
