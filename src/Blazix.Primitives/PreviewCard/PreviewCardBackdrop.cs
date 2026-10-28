using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// PRV-STA-08 (preview-card/backdrop/PreviewCardBackdrop.tsx:26-29). TransitionStatus is the `transitions` module's (ADR 04
// §2): mirrored.
/// <summary>Represents the state of a preview card backdrop, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the preview card is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the preview card's enter or exit transition, or <see langword="null"/>; its <c>data-starting-style</c> and
/// <c>data-ending-style</c> are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record PreviewCardBackdropState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus);

// preview-card/backdrop/PreviewCardBackdropDataAttributes.ts (PRV-DOM-19).
/// <summary>Provides the names of the data attributes a preview card backdrop renders.</summary>
public static class PreviewCardBackdropDataAttributes
{
    /// <summary>Represents the attribute present while the preview card is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the preview card is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the backdrop is animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the backdrop is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

// Base UI's PreviewCard.Backdrop (preview-card/backdrop/PreviewCardBackdrop.tsx); the contract is
// docs/components/preview-card.md §3.4, §5.6, in PreviewCardBackdrop's shape (M4 track 5): role="presentation", hidden while
// unmounted, and pointer-events: none, user-select: none and -webkit-user-select: none always, under the consumer's
// style (PRV-DOM-18); its own TransitionsTwin writes the status attributes and ends its exit as the PreviewCard's does
// (PreviewCardBackdrop's landed precedent, in place of the brief's ExternalExit). It may render inside or outside the Portal,
// anywhere under the Root (PRV-STR-07, PRV §16 Q9).
/// <summary>Represents an overlay beneath a preview card, Base UI's <c>PreviewCard.Backdrop</c>, rendered as a <c>div</c>.</summary>
public sealed class PreviewCardBackdrop : PartBase<PreviewCardBackdropState>
{
    private const string InertStyle = "pointer-events: none; user-select: none; -webkit-user-select: none";
    private const string OpenSelector = "[data-open]";

    private static readonly StateAttributesMapping<PreviewCardBackdropState> s_mapping = PopupStateMapping.CreateOpen<PreviewCardBackdropState>();

    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PreviewCardStoreView? view;
    private PreviewCardBackdropState? state;
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitions;
    private Task? attaching;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PreviewCardRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store here.
    /// <inheritdoc />
    protected override PreviewCardBackdropState State
    {
        get
        {
            bool isOpen = view!.Open;
            TransitionStatus? current = view.TransitionStatus;
            if (state is null || state.Open != isOpen || state.TransitionStatus != current)
            {
                state = new PreviewCardBackdropState(isOpen, current);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<PreviewCardBackdropState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <summary>Gets a task that completes once the backdrop's transitions handle has attached the first time.</summary>
    internal Task TwinAttached => attached.Task;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The backdrop is not inside a <see cref="PreviewCardRoot{TPayload}"/>.</exception>
    protected override void OnInitialized()
    {
        // PRV-EDGE-01.
        PreviewCardRootContext root = PreviewCardRootContext.Require(RootContext);
        view = root.CreateView(StateHasChanged, PreviewCardSelection.Open | PreviewCardSelection.Mounted | PreviewCardSelection.TransitionStatus | PreviewCardSelection.UnmountRequests);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Element is null && Id is null)
        {
            return;
        }

        // The view is read before the portal's move: a backdrop disposed during the await leaves it null.
        PreviewCardStoreView current = view!;
        await PopupPortalContext.WhenMoved(Portal);
        TransitionsOptions options = new()
        {
            Open = current.Open,
            Mounted = current.Mounted,
            Status = current.TransitionStatus,
            UnmountRequests = current.UnmountRequests,
            OpenSelector = OpenSelector,
        };
        transitions ??= new TransitionsTwin(JSRuntime);
        if (transitionsHandle is null)
        {
            attaching ??= AttachAsync(options);
            await attaching;
        }
        else if (options != sentTransitions)
        {
            sentTransitions = options;
            await transitionsHandle.UpdateAsync(options);
        }

        // It selects the mounted state, so it brings the render-batch boundary (ADR 16 Amendments 2026-10-27).
        await current.AtBatchBoundaryAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            view?.Dispose();
            view = null;
            if (transitions is not null)
            {
                _ = transitions.DisposeAsync().AsTask();
                transitions = null;
                transitionsHandle = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "presentation");
        if (!view!.Mounted)
        {
            attributes.Set("hidden", true);
        }

        attributes.Set(MergeProps.StyleAttribute, InertStyle);
    }

    private async Task AttachAsync(TransitionsOptions options)
    {
        sentTransitions = options;
        transitionsHandle = Element is { } element
            ? await transitions!.AttachAsync(element, options)
            : await transitions!.AttachAsync(Id!, options);
        attaching = null;
        attached.TrySetResult();
    }
}
