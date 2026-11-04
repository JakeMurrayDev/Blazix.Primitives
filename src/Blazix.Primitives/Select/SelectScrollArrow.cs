using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// SEL-STA-25 (select/scroll-arrow/SelectScrollArrow.tsx:40, 73-78, 153-170): Base UI's one `SelectScrollArrow.State`, shared
// by both parts (ADR 08 §3's companion table gains it by a dated line). Visible and TransitionStatus are JavaScript's from
// attach (the `select` module writes `data-visible`, the `transitions` module the phase): mirrored. Side is C#'s, from the
// Positioner's (SEL-STA-25: `none` while aligned, §16 Q13).
/// <summary>Represents the state of a select scroll arrow, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Direction">The direction the arrow scrolls the list in, <c>up</c> or <c>down</c>; projected as <c>data-direction</c>.</param>
/// <param name="Visible">A value indicating whether the list can scroll in the arrow's direction; its <c>data-visible</c> is written by the browser-side behavior.</param>
/// <param name="Side">The positioner's side, or <see cref="SelectSide.None"/> while placed over the trigger; projected as <c>data-side</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the arrow's enter or exit transition, or <see langword="null"/>; its <c>data-starting-style</c> and
/// <c>data-ending-style</c> are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record SelectScrollArrowState(string Direction, [property: MirroredField] bool Visible, SelectSide? Side, [property: MirroredField] TransitionStatus? TransitionStatus);

// select/scroll-up-arrow/SelectScrollUpArrowDataAttributes.ts (SEL-DOM-44, 45).
/// <summary>Provides the names of the data attributes a select scroll-up arrow renders.</summary>
public static class SelectScrollUpArrowDataAttributes
{
    /// <summary>Represents the attribute whose value is the arrow's direction: <c>data-direction</c>.</summary>
    public const string Direction = "data-direction";

    /// <summary>Represents the attribute present while the list can scroll up: <c>data-visible</c>.</summary>
    public const string Visible = "data-visible";

    /// <summary>Represents the attribute whose value is the positioner's side: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute present while the arrow is animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the arrow is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

// select/scroll-down-arrow/SelectScrollDownArrowDataAttributes.ts (SEL-DOM-44, 45).
/// <summary>Provides the names of the data attributes a select scroll-down arrow renders.</summary>
public static class SelectScrollDownArrowDataAttributes
{
    /// <summary>Represents the attribute whose value is the arrow's direction: <c>data-direction</c>.</summary>
    public const string Direction = "data-direction";

    /// <summary>Represents the attribute present while the list can scroll down: <c>data-visible</c>.</summary>
    public const string Visible = "data-visible";

    /// <summary>Represents the attribute whose value is the positioner's side: <c>data-side</c>.</summary>
    public const string Side = PopupDataAttributes.Side;

    /// <summary>Represents the attribute present while the arrow is animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the arrow is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

// Base UI's SelectScrollArrow (select/scroll-arrow/SelectScrollArrow.tsx), shared by the two parts (ADR 16 rule 10: an
// internal class, not a component): SEL-STR-10, SEL-DOM-43..45, SEL-ANI-11, SEL-STA-30. The arrow's own transition machine
// on its visibility (TRN §6.1's scroll-arrow row): mounted while visible, through its exit, or kept mounted; the
// `transitions` module reads the open state from `data-visible` (OpenSelector), which the `select` module writes from
// attach; the mount counter of the List's scrollbar class (SEL-STA-30) counts the part's lifetime.
/// <summary>The behavior a select scroll arrow part composes.</summary>
internal sealed class SelectScrollArrowBehavior : IDisposable
{
    private const string OpenSelector = "[data-visible]";

    private readonly IJSRuntime jsRuntime;
    private readonly Action changed;
    private readonly Action<TransitionStatusChange> onStatus;
    private readonly Action<bool> onCompleted;
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private SelectModuleAttachment? module;
    private bool mounted;
    private TransitionStatus? status;
    private bool handoverVisible;
    private bool handedOver;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="SelectScrollArrowBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
    /// <param name="invokeAsync">The part's dispatcher.</param>
    /// <param name="changed">The part's re-render.</param>
    /// <param name="direction">The arrow's direction, <c>up</c> or <c>down</c>.</param>
    public SelectScrollArrowBehavior(IJSRuntime jsRuntime, Func<Action, Task> invokeAsync, Action changed, string direction)
    {
        this.jsRuntime = jsRuntime;
        this.changed = changed;
        Direction = direction;
        onStatus = change => _ = invokeAsync(() => Mirror(change.Status, change.Mounted));
        onCompleted = open => _ = invokeAsync(() => Complete(open));
    }

    /// <summary>Gets the arrow's direction.</summary>
    public string Direction { get; }

    /// <summary>Gets a value indicating whether the list can scroll in the arrow's direction, as C# last heard.</summary>
    public bool Visible { get; private set; }

    /// <summary>Gets a value indicating whether the element stays rendered while the arrow is not visible.</summary>
    public bool KeepMounted { get; private set; }

    /// <summary>Gets a value indicating whether the part renders its element.</summary>
    public bool Shown => KeepMounted || mounted;

    /// <summary>Gets the mirrored transition status.</summary>
    public TransitionStatus? TransitionStatus => status;

    /// <summary>Gets a task that completes when the arrow's handles are attached.</summary>
    public Task TwinAttached => module?.Attached ?? Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (transitions is not null)
        {
            transitions.StatusChanged -= onStatus;
            transitions.TransitionCompleted -= onCompleted;
            _ = transitions.DisposeAsync().AsTask();
            transitions = null;
        }

        if (module is not null)
        {
            _ = module.DisposeAsync().AsTask();
            module = null;
        }
    }

    /// <summary>Applies the visibility and the keep-mounted flag of a render.</summary>
    /// <param name="visible">Whether the list can scroll in the arrow's direction and the popup was not opened by touch.</param>
    /// <param name="keepMounted">Whether the element stays rendered while not visible.</param>
    public void Update(bool visible, bool keepMounted)
    {
        KeepMounted = keepMounted;
        if (visible == Visible)
        {
            return;
        }

        Visible = visible;
        if (visible && !mounted)
        {
            // An arrow that becomes visible is created in its starting phase (TRN-ALG-01).
            mounted = true;
            status = Primitives.TransitionStatus.Starting;
        }
        else if (!visible && mounted && transitionsHandle is null)
        {
            mounted = false;
            status = null;
        }
    }

    /// <summary>Writes the attributes the arrow owns: <c>aria-hidden</c>, the position style, and the handover's <c>data-visible</c>.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        attributes.Set("aria-hidden", "true");
        attributes.Set(MergeProps.StyleAttribute, "position: absolute");
        if (!handedOver)
        {
            handoverVisible = Visible;
        }

        if (handoverVisible)
        {
            attributes.Set(SelectScrollUpArrowDataAttributes.Visible, string.Empty);
        }

        if (!handedOver && status == Primitives.TransitionStatus.Starting)
        {
            attributes.Set(PopupDataAttributes.StartingStyle, string.Empty);
        }
    }

    /// <summary>Attaches the arrow's handles after a render.</summary>
    /// <param name="element">The arrow's element, or <see langword="null"/> when it rendered none.</param>
    /// <param name="positioner">The positioner's element.</param>
    /// <param name="portal">The enclosing portal, whose move the attach waits for.</param>
    /// <returns>A task that completes when the handles are attached or updated.</returns>
    public async Task AfterRenderAsync(ElementReference? element, ElementReference? positioner, PopupPortalContext? portal)
    {
        if (disposed || element is not { } rendered)
        {
            return;
        }

        await PopupPortalContext.WhenMoved(portal);
        if (disposed)
        {
            return;
        }

        module ??= new SelectModuleAttachment(jsRuntime);
        await module.SyncAsync(rendered, new SelectOptions { Part = SelectTwin.ScrollArrowPart, Direction = Direction }, positioner: positioner);
        if (transitions is null)
        {
            transitions = new TransitionsTwin(jsRuntime);
            transitions.StatusChanged += onStatus;
            transitions.TransitionCompleted += onCompleted;
            transitionsHandle = await transitions.AttachAsync(rendered, new TransitionsOptions
            {
                Open = Visible,
                Mounted = mounted,
                Status = status,
                KeepMounted = KeepMounted,
                OpenSelector = OpenSelector,
            });
            handedOver = true;
        }
    }

    private void Mirror(TransitionStatus? next, bool nextMounted)
    {
        if (disposed || (next == status && nextMounted == mounted))
        {
            return;
        }

        status = next;
        mounted = nextMounted;
        changed();
    }

    // SEL-ANI-11: the exit finished, so the arrow unmounts (unless kept).
    private void Complete(bool open)
    {
        if (disposed || open || Visible)
        {
            return;
        }

        mounted = false;
        status = null;
        changed();
    }
}

// Base UI's Select.ScrollUpArrow and Select.ScrollDownArrow (select/scroll-up-arrow, select/scroll-down-arrow); SEL-API-47:
// one implementation with the direction fixed, `▲`/`▼` by default.
/// <summary>Represents an arrow that scrolls a select's list up while hovered, Base UI's <c>Select.ScrollUpArrow</c>, rendered as a <c>div</c>.</summary>
/// <remarks>It renders while the list can scroll up, unless <see cref="KeepMounted"/> keeps it. Without content it shows ▲.</remarks>
public sealed class SelectScrollUpArrow : PartBase<SelectScrollArrowState>
{
    private static readonly RenderFragment s_glyph = static builder => builder.AddContent(0, "▲");

    private SelectScrollArrowBehavior? behavior;
    private SelectStore? store;
    private StoreSelection<SelectStoreState, bool>? visible;
    private StoreSelection<SelectStoreState, bool>? touchOpened;
    private SelectScrollArrowState? state;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private SelectStore? RootStore { get; set; }

    [CascadingParameter]
    private SelectPositionerContext? PositionerContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <summary>Gets or sets a value indicating whether the arrow stays in the DOM while the list cannot scroll up.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool KeepMounted { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override SelectScrollArrowState State => SelectScrollArrowParts.State(ref state, behavior!, PositionerContext!);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The arrow is not inside a <see cref="SelectRoot{TValue}"/> and a <see cref="SelectPositioner"/>.</exception>
    protected override void OnInitialized()
    {
        store = SelectStore.Require(RootStore);
        SelectPositionerContext.Require(PositionerContext);
        behavior = new SelectScrollArrowBehavior(JSRuntime, action => InvokeAsync(action), () => { state = null; StateHasChanged(); }, "up");
        store.CountScrollArrow(true);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ChildContent ??= s_glyph;
        behavior!.Update(visible!.Value && !touchOpened!.Value, KeepMounted);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        behavior!.Update(visible!.Value && !touchOpened!.Value, KeepMounted);
        if (behavior.Shown)
        {
            base.BuildRenderTree(builder);
        }
        else
        {
            ((IPartHost)this).SetElement(null);
        }
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(behavior.Shown ? Element : null, PositionerContext!.Element, Portal);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            store?.CountScrollArrow(false);
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SelectStoreState> subscription = SubscribeTo<SelectStoreState>(store!);
        visible = subscription.Select(SelectStoreState.ScrollUpVisible);
        touchOpened = subscription.Select(SelectStoreState.TouchOpened);
        subscription.Select(SelectStoreState.AlignedActiveSelector);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);
}

/// <summary>Represents an arrow that scrolls a select's list down while hovered, Base UI's <c>Select.ScrollDownArrow</c>, rendered as a <c>div</c>.</summary>
/// <remarks>It renders while the list can scroll down, unless <see cref="KeepMounted"/> keeps it. Without content it shows ▼.</remarks>
public sealed class SelectScrollDownArrow : PartBase<SelectScrollArrowState>
{
    private static readonly RenderFragment s_glyph = static builder => builder.AddContent(0, "▼");

    private SelectScrollArrowBehavior? behavior;
    private SelectStore? store;
    private StoreSelection<SelectStoreState, bool>? visible;
    private StoreSelection<SelectStoreState, bool>? touchOpened;
    private SelectScrollArrowState? state;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private SelectStore? RootStore { get; set; }

    [CascadingParameter]
    private SelectPositionerContext? PositionerContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <summary>Gets or sets a value indicating whether the arrow stays in the DOM while the list cannot scroll down.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool KeepMounted { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override SelectScrollArrowState State => SelectScrollArrowParts.State(ref state, behavior!, PositionerContext!);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The arrow is not inside a <see cref="SelectRoot{TValue}"/> and a <see cref="SelectPositioner"/>.</exception>
    protected override void OnInitialized()
    {
        store = SelectStore.Require(RootStore);
        SelectPositionerContext.Require(PositionerContext);
        behavior = new SelectScrollArrowBehavior(JSRuntime, action => InvokeAsync(action), () => { state = null; StateHasChanged(); }, "down");
        store.CountScrollArrow(true);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ChildContent ??= s_glyph;
        behavior!.Update(visible!.Value && !touchOpened!.Value, KeepMounted);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        behavior!.Update(visible!.Value && !touchOpened!.Value, KeepMounted);
        if (behavior.Shown)
        {
            base.BuildRenderTree(builder);
        }
        else
        {
            ((IPartHost)this).SetElement(null);
        }
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(behavior.Shown ? Element : null, PositionerContext!.Element, Portal);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            store?.CountScrollArrow(false);
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SelectStoreState> subscription = SubscribeTo<SelectStoreState>(store!);
        visible = subscription.Select(SelectStoreState.ScrollDownVisible);
        touchOpened = subscription.Select(SelectStoreState.TouchOpened);
        subscription.Select(SelectStoreState.AlignedActiveSelector);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);
}

/// <summary>The state record both scroll arrow parts build.</summary>
internal static class SelectScrollArrowParts
{
    /// <summary>Returns the arrow's state record, built again only when a field changed.</summary>
    /// <param name="state">The cached record.</param>
    /// <param name="behavior">The arrow's behavior.</param>
    /// <param name="positioner">The positioner's context.</param>
    /// <returns>The record.</returns>
    public static SelectScrollArrowState State(ref SelectScrollArrowState? state, SelectScrollArrowBehavior behavior, SelectPositionerContext positioner)
    {
        SelectSide? side = positioner.Side;
        if (state is null || state.Visible != behavior.Visible || state.Side != side || state.TransitionStatus != behavior.TransitionStatus)
        {
            state = new SelectScrollArrowState(behavior.Direction, behavior.Visible, side, behavior.TransitionStatus);
        }

        return state;
    }
}
