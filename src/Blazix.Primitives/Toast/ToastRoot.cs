using System.Globalization;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// TST §3.4 state object (toast/root/ToastRoot.tsx:497-504); TST-STA-26. Three fields are mirrored: the `transitions`
// module writes data-starting-style and data-ending-style from the transition status, and the `toast` module writes
// data-swiping and data-swipe-direction (M6 track 9b); C# projects none of them.
/// <summary>Represents the state of a toast's root, which the part exposes as data attributes.</summary>
/// <param name="TransitionStatus">The toast's enter or exit phase, or <see langword="null"/> while it is settled.</param>
/// <param name="Expanded">A value indicating whether the stack of toasts is expanded.</param>
/// <param name="Limited">A value indicating whether the toast is beyond the provider's limit.</param>
/// <param name="Type">The toast's type, or <see langword="null"/>.</param>
/// <param name="Swiping">A value indicating whether a swipe gesture is in progress.</param>
/// <param name="SwipeDirection">The armed or dismissing swipe direction, or <see langword="null"/>.</param>
[PartState]
public sealed partial record ToastRootState(
    [property: MirroredField] TransitionStatus? TransitionStatus,
    bool Expanded,
    bool Limited,
    string? Type,
    [property: MirroredField] bool Swiping,
    [property: MirroredField] SwipeDirection? SwipeDirection);

// toast/root/ToastRootDataAttributes.ts:7-27 (TST-DOM-06, TST-DOM-07).
/// <summary>Provides the data attributes a toast's root renders.</summary>
public static class ToastRootDataAttributes
{
    /// <summary>Present while the stack of toasts is expanded.</summary>
    public const string Expanded = "data-expanded";

    /// <summary>Present while the toast is beyond the provider's limit.</summary>
    public const string Limited = "data-limited";

    /// <summary>The toast's type, when it has one.</summary>
    public const string Type = "data-type";

    /// <summary>Present while a swipe gesture is in progress.</summary>
    public const string Swiping = "data-swiping";

    /// <summary>The armed or dismissing swipe direction: <c>up</c>, <c>down</c>, <c>left</c> or <c>right</c>.</summary>
    public const string SwipeDirection = "data-swipe-direction";

    /// <summary>Present from the toast's add until it is first measured.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Present while the toast is closing.</summary>
    public const string EndingStyle = "data-ending-style";
}

// toast/root/ToastRootCssVars.ts:5-25 (TST-DOM-10..13).
/// <summary>Provides the CSS variables a toast's root sets.</summary>
public static class ToastRootCssVars
{
    /// <summary>The toast's position among the toasts that are not closing, newest first; while closing, its position in the list.</summary>
    public const string Index = "--toast-index";

    /// <summary>The sum of the heights of the toasts before this one, in pixels.</summary>
    public const string OffsetY = "--toast-offset-y";

    /// <summary>The toast's measured height, in pixels, once it is measured and while it is not closing.</summary>
    public const string Height = "--toast-height";

    /// <summary>The horizontal swipe offset, in pixels; <c>0px</c> at rest.</summary>
    public const string SwipeMovementX = "--toast-swipe-movement-x";

    /// <summary>The vertical swipe offset, in pixels; <c>0px</c> at rest.</summary>
    public const string SwipeMovementY = "--toast-swipe-movement-y";
}

// Base UI's Toast.Root (toast/root/ToastRoot.tsx); TST-STR-03, TST-API-13, TST-DOM-06, TST-DOM-08..12, TST-ARIA-02..06,
// TST-EDGE-10. Every lookup is by the toast's id (STR-03, EDGE-10): the Root selects its toast, its indexes and its offset
// from the Provider's store by id, so a consumer may pass a fresh copy on every render, and it reads the store's copy when
// there is one. C# renders the role, tabindex, aria-modal, the label references, the high-priority aria-hidden, inert, the
// state attributes and, until the `toast` module takes the style over at attach (M6 track 9b; ADR 04 §2), the stack
// variables. The Root publishes a non-generic view of its toast to its parts (ToastRootContext).
// ADR 08 Amendments 2026-11-18 (chosen by the user 2026-11-18): TData is inferred through the Toast parameter.
/// <summary>Represents one toast. Base UI's <c>Toast.Root</c>; it renders a <c>div</c>.</summary>
/// <typeparam name="TData">The type of the custom data the toast carries.</typeparam>
public sealed class ToastRoot<TData> : PartBase<ToastRootState>
{
    private readonly RenderFragment renderPart;
    private ToastProviderContext<TData>? provider;
    private ToastRootContext? context;
    private SelectorSubscription<ToastStoreState<TData>>? subscription;
    private StoreSelection<ToastStoreState<TData>, string, ToastObject<TData>?>? stored;
    private StoreSelection<ToastStoreState<TData>, string, int>? domIndex;
    private StoreSelection<ToastStoreState<TData>, string, int>? visibleIndex;
    private StoreSelection<ToastStoreState<TData>, string, double>? offsetY;
    private StoreSelection<ToastStoreState<TData>, bool>? expanded;
    private StoreSelection<ToastStoreState<TData>, bool>? focused;
    private RootView rendered;
    private RootView view;
    private static readonly IReadOnlyList<SwipeDirection> s_defaultSwipeDirections = [Primitives.SwipeDirection.Down, Primitives.SwipeDirection.Right];
    private static readonly IReadOnlyList<SwipeDirection> s_noSwipeDirections = [];

    private ToastRootState state = new(null, false, false, null, false, null);

    /// <summary>Initializes a new instance of the <see cref="ToastRoot{TData}"/> class.</summary>
    public ToastRoot()
    {
        renderPart = RenderPart;
    }

    // The store's copy of the toast when it holds one, else the parameter.
    private ToastObject<TData> Current => view.Toast ?? Toast;

    [CascadingParameter]
    private ToastProviderContext<TData>? ProviderContext { get; set; }

    /// <summary>Gets or sets the toast to render.</summary>
    /// <value>
    /// The toast, typically one of the viewport's toasts. It is found by its <see cref="ToastObject{TData}.Id"/>, so a copy
    /// of it may be passed.
    /// </value>
    [EditorRequired]
    [Parameter]
    public ToastObject<TData> Toast { get; set; } = null!;

    // TST-API-14, TST-I18N-01: ordered (a release takes the first allowed direction in the order given), physical, so a
    // list rather than a [Flags] enum; Base UI's single string is a one-element list.
    /// <summary>Gets or sets the directions in which a swipe dismisses the toast.</summary>
    /// <value>
    /// The directions, in the order a release tries them; or <see langword="null"/> (the default) for down, then right. The
    /// directions are physical. A toast whose positioner settings set an anchor cannot be swiped away.
    /// </value>
    [Parameter]
    public IReadOnlyList<SwipeDirection>? SwipeDirections { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ToastRootState State
    {
        get
        {
            ToastObject<TData> toast = Current;
            bool isExpanded = view.Expanded;
            // The swipe fields are the module's reports (M6 track 9b); until then they stay at rest.
            if (state.TransitionStatus != toast.TransitionStatus || state.Expanded != isExpanded || state.Limited != toast.Limited || !string.Equals(state.Type, toast.Type, StringComparison.Ordinal))
            {
                state = state with { TransitionStatus = toast.TransitionStatus, Expanded = isExpanded, Limited = toast.Limited, Type = toast.Type };
            }

            return state;
        }
    }

    // TST-API-14, TST-PTR-22: the directions the `toast` module's gesture uses (M6 track 9b); an anchored toast has none.
    internal IReadOnlyList<SwipeDirection> EffectiveSwipeDirections =>
        Current.Positioner is { HasAnchor: true } ? s_noSwipeDirections : SwipeDirections ?? s_defaultSwipeDirections;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        provider = ToastProviderContext<TData>.Require(ProviderContext);
        ToastStore<TData> store = provider.Store;
        context = new ToastRootContext(id => store.CloseToast(id));
        context.TitleId.Changed = StateHasChanged;
        context.DescriptionId.Changed = StateHasChanged;
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Toast);

        // TST-ANI-08: a Root reused for another toast (an index-keyed list) follows the new id.
        string id = Toast.Id;
        stored!.Arg = id;
        domIndex!.Arg = id;
        visibleIndex!.Arg = id;
        offsetY!.Arg = id;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // The Viewport's render reaches this Root before the Root's own subscription is notified of the same write, so the
        // Root reads the store's current state, not its selections' last values.
        view = Read();
        rendered = view;
        context!.Publish(Current, view.Expanded, view.VisibleIndex);
        builder.OpenComponent<CascadingValue<ToastRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ToastRootContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<ToastRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ToastRootContext>.ChildContent), renderPart);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender) => context!.Flush();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscription?.Dispose();
            subscription = null;
            if (context is not null)
            {
                context.TitleId.Changed = null;
                context.DescriptionId.Changed = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        // One subscription (ADR 05 §1), whose callback renders only when what the Root last rendered changed: a write that
        // also renders the Viewport has already rendered this Root (the FieldRoot precedent; ADR 16 rule 8).
        subscription = new SelectorSubscription<ToastStoreState<TData>>(provider!.Store, OnSelectionChanged);
        string id = Toast?.Id ?? string.Empty;
        stored = subscription.Select(ToastSelectors<TData>.Toast, id);
        domIndex = subscription.Select(ToastSelectors<TData>.ToastIndex, id);
        visibleIndex = subscription.Select(ToastSelectors<TData>.ToastVisibleIndex, id);
        offsetY = subscription.Select(ToastSelectors<TData>.ToastOffsetY, id);
        expanded = subscription.Select(ToastSelectors<TData>.Expanded);
        focused = subscription.Select(ToastSelectors<TData>.Focused);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        ToastObject<TData> toast = Current;
        bool high = toast.Priority == ToastPriority.High;

        // TST-ARIA-02, TST-DOM-08, TST-ARIA-03..06.
        attributes.Set("role", high ? "alertdialog" : "dialog");
        attributes.Set("tabindex", "0");
        attributes.Set("aria-modal", "false");
        if (context!.TitleId.Id is { } titleId)
        {
            attributes.Set("aria-labelledby", titleId);
        }

        if (context.DescriptionId.Id is { } descriptionId)
        {
            attributes.Set("aria-describedby", descriptionId);
        }

        if (high && !view.Focused)
        {
            attributes.Set("aria-hidden", "true");
        }

        // TST-DOM-09 (UTL-DOM-16): present and empty while limited.
        if (toast.Limited)
        {
            attributes.Set("inert", string.Empty);
        }

        // TST-DOM-10..12: the visible index, or the DOM index while ending; the offset; the height when positive.
        int index = toast.TransitionStatus == TransitionStatus.Ending ? view.DomIndex : view.VisibleIndex;
        string style = toast.Height is > 0 and double height
            ? string.Create(CultureInfo.InvariantCulture, $"{ToastRootCssVars.Index}: {index}; {ToastRootCssVars.OffsetY}: {view.OffsetY}px; {ToastRootCssVars.Height}: {height}px")
            : string.Create(CultureInfo.InvariantCulture, $"{ToastRootCssVars.Index}: {index}; {ToastRootCssVars.OffsetY}: {view.OffsetY}px");
        attributes.Set("style", style);
    }

    private void RenderPart(RenderTreeBuilder builder) => base.BuildRenderTree(builder);

    private RootView Read()
    {
        ToastStoreState<TData> current = provider!.Store.State;
        string id = Toast.Id;
        return new RootView(
            ToastSelectors<TData>.Toast(current, id),
            ToastSelectors<TData>.ToastIndex(current, id),
            ToastSelectors<TData>.ToastVisibleIndex(current, id),
            ToastSelectors<TData>.ToastOffsetY(current, id),
            ToastSelectors<TData>.Expanded(current),
            ToastSelectors<TData>.Focused(current));
    }

    private void OnSelectionChanged()
    {
        if (!Read().Equals(rendered))
        {
            StateHasChanged();
        }
    }

    // What the Root renders from the store; the toast compares as a record, so an equal copy renders nothing new.
    private readonly record struct RootView(ToastObject<TData>? Toast, int DomIndex, int VisibleIndex, double OffsetY, bool Expanded, bool Focused);
}
