using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Utilities;

// RND-STR-10, RND-API-12, RND-DOM-50..52, RND-ARIA-01, RND-ANI-01..03 (utils/ItemIndicator.tsx): the behaviour behind
// Select.ItemIndicator and Combobox.ItemIndicator, composed by their sealed shells (design point 1 of M5 track 4, settled by
// the orchestrator 2026-11-04: shared internal part behaviours live in Utilities/). RND-ANI-01's mounting decision is the
// behaviour's: the element is rendered while selected or kept mounted, and through its exit; the shell passes only Selected,
// from its item, and KeepMounted. The exit runs through a TransitionsTwin with OpenSelector "[data-selected]" (the
// RadioIndicator precedent): C# renders the initial status under the handover and never emulates frames. Base UI's
// useOpenChangeComplete is enabled only for a deselection, so the enter completion is not reported.
/// <summary>Drives the mounting and the transition of an item's selection indicator.</summary>
internal sealed class ItemIndicatorBehavior
{
    /// <summary>Represents the selector the transitions module reads the indicator's open state from.</summary>
    public const string OpenSelector = "[data-selected]";

    /// <summary>Represents the default content, a heavy check mark.</summary>
    public static readonly RenderFragment DefaultContent = builder => builder.AddContent(0, "✔️");

    private readonly IJSRuntime jsRuntime;
    private readonly Action changed;
    private readonly Action<TransitionStatusChange> onStatus;
    private readonly Action<bool> onCompleted;
    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TransitionsTwin? twin;
    private TwinHandle<TransitionsOptions>? handle;
    private TransitionsOptions? sent;
    private ElementReference? element;
    private bool mounted;
    private TransitionStatus? status;
    private TransitionStatus? renderedStatus;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ItemIndicatorBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
    /// <param name="invokeAsync">The part's dispatcher, which runs a twin report on the renderer.</param>
    /// <param name="changed">The part's re-render, run after a report changed the state.</param>
    public ItemIndicatorBehavior(IJSRuntime jsRuntime, Func<Action, Task> invokeAsync, Action changed)
    {
        this.jsRuntime = jsRuntime;
        this.changed = changed;
        onStatus = change => _ = invokeAsync(() => Mirror(change.Status, change.Mounted));
        onCompleted = open => _ = invokeAsync(() => Complete(open));
    }

    /// <summary>Gets a value indicating whether the item is selected.</summary>
    public bool Selected { get; private set; }

    /// <summary>Gets a value indicating whether the element stays rendered while the item is not selected.</summary>
    public bool KeepMounted { get; private set; }

    /// <summary>Gets a value indicating whether the part renders its element.</summary>
    public bool Shown => KeepMounted || mounted;

    /// <summary>Gets the mirrored transition status of the element.</summary>
    public TransitionStatus? TransitionStatus => status;

    /// <summary>Gets a task that completes once the element's transitions handle has attached the first time.</summary>
    public Task TwinAttached => attached.Task;

    /// <summary>Releases the transitions twin.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (twin is not null)
        {
            twin.StatusChanged -= onStatus;
            twin.TransitionCompleted -= onCompleted;
            _ = twin.DisposeAsync().AsTask();
            twin = null;
        }

        handle = null;
    }

    /// <summary>Applies the first parameters: an indicator selected at mount is mounted, without a starting phase.</summary>
    /// <param name="selected">Whether the item is selected.</param>
    /// <param name="keepMounted">Whether the element stays rendered while the item is not selected.</param>
    public void Initialize(bool selected, bool keepMounted)
    {
        Selected = selected;
        KeepMounted = keepMounted;
        (mounted, status) = TransitionStatusRules.Initial(selected, enableIdleState: false, animateInitialOpen: false);
    }

    /// <summary>Applies new parameters.</summary>
    /// <param name="selected">Whether the item is selected.</param>
    /// <param name="keepMounted">Whether the element stays rendered while the item is not selected.</param>
    /// <returns><see langword="true"/> when the state the part renders changed.</returns>
    public bool Update(bool selected, bool keepMounted)
    {
        bool selectionChanged = selected != Selected;
        bool keepChanged = keepMounted != KeepMounted;
        Selected = selected;
        KeepMounted = keepMounted;
        if (selectionChanged)
        {
            ApplyOpenState();
        }

        return selectionChanged || keepChanged;
    }

    /// <summary>Writes the attributes the indicator owns.</summary>
    /// <param name="attributes">The part's attributes, before the consumer's are merged, so a consumer's <c>aria-hidden</c> wins.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("aria-hidden", "true");

        // The handover value of an element created selected (RND-ANI-02): the module owns the attribute from attach on.
        if (renderedStatus == Blazix.Primitives.TransitionStatus.Starting)
        {
            attributes.Set(ItemIndicatorDataAttributes.StartingStyle, string.Empty);
        }
    }

    /// <summary>Attaches or updates the element's transitions handle after a render.</summary>
    /// <param name="rendered">The part's element, or <see langword="null"/> when it rendered none.</param>
    /// <returns>A task that completes when the handle is attached or updated.</returns>
    public async Task AfterRenderAsync(ElementReference? rendered)
    {
        if (disposed)
        {
            return;
        }

        element = Shown ? rendered : null;
        if (element is null || (handle is not null && !string.Equals(handle.Key, element.Value.Id, StringComparison.Ordinal)))
        {
            if (handle is { } released)
            {
                handle = null;
                sent = null;
                await released.DisposeAsync();
            }

            if (element is null)
            {
                return;
            }
        }

        if (handle is not null)
        {
            // With OpenSelector the module reads the selection from the applied batch; only KeepMounted is sent again.
            if (sent!.KeepMounted != KeepMounted)
            {
                sent = sent with { KeepMounted = KeepMounted };
                await handle.UpdateAsync(sent);
            }

            return;
        }

        if (twin is null)
        {
            twin = new TransitionsTwin(jsRuntime);
            twin.StatusChanged += onStatus;
            twin.TransitionCompleted += onCompleted;
        }

        // Mounted and Status are what the element was rendered with, read on attach only.
        sent = new TransitionsOptions
        {
            Open = Selected,
            Mounted = mounted,
            Status = renderedStatus,
            KeepMounted = KeepMounted,
            OpenSelector = OpenSelector,
        };
        handle = await twin.AttachAsync(element.Value, sent);
        attached.TrySetResult();
    }

    // The C# half of the transition machine for this render (TransitionStatusRules): an element created selected starts in
    // 'starting'; an element with no transitions handle to animate it leaves at once.
    private void ApplyOpenState()
    {
        bool creating = Selected && !mounted && (!KeepMounted || handle is null);
        (bool nextMounted, TransitionStatus? nextStatus) = TransitionStatusRules.Evaluate(Selected, mounted, status, deferEndingState: false);
        if (creating)
        {
            renderedStatus = nextStatus;
        }
        else if (!Selected && mounted && handle is null && element is null)
        {
            nextMounted = false;
            nextStatus = null;
            renderedStatus = null;
        }

        mounted = nextMounted;
        status = nextStatus;
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

    // RND-ANI-03: the exit finished, so the element unmounts (unless kept) and its status is unset.
    private void Complete(bool open)
    {
        if (disposed || open || Selected)
        {
            return;
        }

        mounted = false;
        status = null;
        renderedStatus = null;
        changed();
    }
}

// RND-STR-10's state (ItemIndicatorState in utils/ItemIndicator.tsx): `selected` is projected as data-selected, never a
// `selected` attribute; the transition status is the transitions module's (mirrored).
/// <summary>Holds the state of an item's selection indicator.</summary>
/// <param name="Selected">Whether the item is selected; rendered as <c>data-selected</c>.</param>
/// <param name="TransitionStatus">The phase of the indicator's transition, written by the transitions module.</param>
[PartState]
internal sealed partial record ItemIndicatorState(
    bool Selected,
    [property: MirroredField] TransitionStatus? TransitionStatus);

/// <summary>Provides the data attributes of an item's selection indicator.</summary>
internal static class ItemIndicatorDataAttributes
{
    /// <summary>Represents the attribute present while the item is selected.</summary>
    public const string Selected = "data-selected";

    /// <summary>Represents the attribute present while the indicator is animating in.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the indicator is animating out.</summary>
    public const string EndingStyle = "data-ending-style";
}
