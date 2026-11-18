using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// DRW §3.10, DRW-STA-01: Dialog.Viewport's state object (dialog/viewport/DialogViewport.tsx:35-40) under its Drawer name.
// TransitionStatus is mirrored (ADR 04 §2). NestedDialogOpen is a field but never an attribute (DRW-DOM-22).
/// <summary>Represents the state of a drawer's viewport, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the drawer is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the drawer's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
/// <param name="Nested">A value indicating whether the drawer is nested in another dialog; projected as <c>data-nested</c>.</param>
/// <param name="NestedDialogOpen">A value indicating whether a dialog nested in this one is open; not projected as an attribute.</param>
[PartState]
public sealed partial record DrawerViewportState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus, bool Nested, bool NestedDialogOpen);

// DRW-STR-02 (drawer/viewport/DrawerViewportDataAttributes.ts): Dialog's without data-nested-dialog-open (DRW-DOM-22).
/// <summary>Provides the names of the data attributes a drawer's viewport renders.</summary>
public static class DrawerViewportDataAttributes
{
    /// <summary>Represents the attribute present while the drawer is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute present while the drawer is closed: <c>data-closed</c>.</summary>
    public const string Closed = "data-closed";

    /// <summary>Represents the attribute present while the drawer begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the drawer is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";

    /// <summary>Represents the attribute present while the drawer is nested in another dialog: <c>data-nested</c>.</summary>
    public const string Nested = "data-nested";
}

// DRW-STR-02 (drawer/viewport/DrawerViewportCssVars.ts).
/// <summary>Provides the names of the CSS variables a drawer's viewport sets.</summary>
public static class DrawerViewportCssVars
{
    /// <summary>
    /// Represents how far the on-screen keyboard overlaps the viewport in pixels, set while a
    /// <see cref="DrawerVirtualKeyboardProvider"/> keeps a field visible: <c>--drawer-keyboard-inset</c>.
    /// </summary>
    public const string KeyboardInset = "--drawer-keyboard-inset";
}

// Base UI's Drawer.Viewport (drawer/viewport/DrawerViewport.tsx:83-1062); the contract is docs/components/drawer.md (§3.10;
// DRW-STR-04 over dialog.md §3.5, §5.4). Its own type over DialogViewportBehavior (decision 20 (a) of the M6 plan, chosen by
// the user 2026-11-18), cascading the context its popup reads (DRW-STR-04, EDGE-01). The gesture, the touch arbitration and
// the keyboard role are the `drawer` module's Viewport handle (8b, 8c).
/// <summary>
/// Represents a positioning container for the drawer's popup that can be made scrollable, Base UI's
/// <c>Drawer.Viewport</c>, rendered as a <c>div</c>.
/// </summary>
/// <remarks>Place it inside the drawer's portal, around the popup.</remarks>
public sealed class DrawerViewport : PartBase<DrawerViewportState>
{
    // DRW-DOM-22: the popup's open mapping, and nestedDialogOpen never projected (Nested keeps the default, data-nested).
    private static readonly StateAttributesMapping<DrawerViewportState> s_mapping = PopupStateMapping.CreateOpen<DrawerViewportState>()
        .Map<bool>(nameof(DrawerViewportState.NestedDialogOpen), static _ => null);

    private readonly DrawerViewportContext viewport = new();
    private RenderFragment? consumerContent;
    private RenderFragment? cascade;
    private DialogViewportBehavior? behavior;
    private DrawerViewportState state = new(false, null, false, false);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    [CascadingParameter]
    private DrawerRootContext? Drawer { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override DrawerViewportState State
    {
        get
        {
            IDialogRootContext root = behavior!.Context;
            bool nestedOpen = root.NestedOpenDialogCount > 0;
            if (state.Open != root.Open || state.TransitionStatus != root.TransitionStatus || state.Nested != root.Nested || state.NestedDialogOpen != nestedOpen)
            {
                state = new DrawerViewportState(root.Open, root.TransitionStatus, root.Nested, nestedOpen);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<DrawerViewportState>? StateAttributesMapping => s_mapping;

    // For the fixtures: completes once the transitions handle is attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    // For the tests: the context the popup reads.
    internal DrawerViewportContext ViewportContext => viewport;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // DRW-EDGE-02 (DrawerViewport.tsx:100): the Drawer root's own state is required too.
        if (Context is null || Drawer is null)
        {
            throw DialogParts.MissingRoot("Drawer");
        }

        behavior = new DialogViewportBehavior("Drawer", JSRuntime, StateHasChanged, InvokeAsync);
        base.OnInitialized();
    }

    // DRW-STR-04: the viewport's context around the consumer's content, rebuilt only when the content changes.
    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (cascade is null || !ReferenceEquals(consumerContent, ChildContent))
        {
            consumerContent = ChildContent;
            cascade = viewport.Cascade(consumerContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Enabled, Element, Portal);

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
    protected override void SubscribeSelectors() => behavior!.Subscribe(Context!, Portal);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);

    // DRW-DOM-22 (DrawerViewport.tsx:1052-1054): the attribute is never rendered, even when the consumer passes it; the
    // document names it part-owned, so the part's removal wins over the consumer (ADR 02 Amendments 2026-10-21).
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => attributes.Remove("data-nested-dialog-open");
}
