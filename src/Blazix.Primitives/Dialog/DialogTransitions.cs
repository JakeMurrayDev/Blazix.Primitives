using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// DLG §9, TRN-ALG-01 (ADR 04 §2): the `transitions` handle of the Backdrop, the Viewport and the Popup, which write their
// own data-starting-style and data-ending-style from attach on (C# renders the initial status under the handover). Each
// reads its open state from `[data-open]` after every render (TRN, OpenSelector), with no idle state and no deferred
// ending. Only the Popup's exit is awaited (DLG-ANI-03): the Backdrop and the Viewport end theirs when the store's unmount
// moves UnmountRequests (ExternalExit, the Collapsible Root and Trigger shape). The Popup reports its finished enter and
// exit, and mirrors its status into the store. A portaled part attaches once the portal has moved its node. The options
// are built only for the attach and when UnmountRequests moved (ADR 16 rule 7).
// The handover at attach (ADR 04 Amendments 2026-10-18 and 2026-10-21): until the handle attaches, C# renders the status
// attributes itself, so the opening render already carries data-starting-style; the attach sends the status the element was
// rendered with, and from then on the part renders the values it rendered last (frozen), so a diff never fights the module
// (the CollapsiblePanelBehavior precedent).
/// <summary>The <c>transitions</c> handle of one dialog part.</summary>
internal sealed class DialogTransitions : IAsyncDisposable
{
    private const string OpenSelector = "[data-open]";
    private const string StartingStyle = "data-starting-style";
    private const string EndingStyle = "data-ending-style";

    private readonly IJSRuntime jsRuntime;
    private readonly bool externalExit;
    private readonly Action<TransitionStatus?>? statusChanged;
    private readonly Func<bool, Task>? completed;
    private readonly Func<Func<Task>, Task> invokeAsync;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TransitionsTwin? twin;
    private TwinHandle<TransitionsOptions>? handle;
    private Task? attaching;
    private int generation;
    private int sentUnmountRequests;
    private bool frozen;
    private bool renderedStarting;
    private bool renderedEnding;
    private TransitionStatus? renderedStatus;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="DialogTransitions"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="externalExit">Whether the store's unmount ends this element's exit, as for the Backdrop and the Viewport.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer, as <c>ComponentBase.InvokeAsync</c>.</param>
    /// <param name="statusChanged">Mirrors a reported status into the store, or <see langword="null"/>.</param>
    /// <param name="completed">Applies a finished enter or exit, or <see langword="null"/> for a part that reports none.</param>
    public DialogTransitions(IJSRuntime jsRuntime, bool externalExit, Func<Func<Task>, Task> invokeAsync, Action<TransitionStatus?>? statusChanged = null, Func<bool, Task>? completed = null)
    {
        this.jsRuntime = jsRuntime;
        this.externalExit = externalExit;
        this.invokeAsync = invokeAsync;
        this.statusChanged = statusChanged;
        this.completed = completed;
    }

    /// <summary>Gets a task that completes once the handle has attached the first time.</summary>
    public Task Attached => attachedSource.Task;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await DetachAsync();
        if (twin is not null)
        {
            await twin.DisposeAsync();
        }
    }

    /// <summary>Adds the status attributes the part renders: its own until the handle attaches, then the frozen ones.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="status">The derived transition status of this render.</param>
    public void AddStatusAttributes(AttributeList attributes, TransitionStatus? status)
    {
        if (!frozen)
        {
            renderedStarting = status == TransitionStatus.Starting;
            renderedEnding = status == TransitionStatus.Ending;
            renderedStatus = status;
        }

        if (renderedStarting)
        {
            attributes.Set(StartingStyle, string.Empty);
        }

        if (renderedEnding)
        {
            attributes.Set(EndingStyle, string.Empty);
        }
    }

    /// <summary>Attaches after the first render that showed the element, sends a moved unmount count, or detaches.</summary>
    /// <param name="element">The element, or <see langword="null"/> when none was rendered.</param>
    /// <param name="context">The root's context.</param>
    /// <param name="keepMounted">Whether the element stays in the document while not mounted.</param>
    /// <param name="ready">The task the first attach awaits, such as the portal's move.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task SyncAsync(ElementReference? element, IDialogRootContext context, bool keepMounted, Task ready)
    {
        if (disposed)
        {
            return;
        }

        if (element is not { } target)
        {
            await DetachAsync();
            return;
        }

        if (handle is null)
        {
            // A render during the pending first attach awaits it: TwinBase hands a repeated attach the pending handle.
            attaching ??= AttachAsync(target, context, keepMounted, ready);
            await attaching;
            return;
        }

        if (context.UnmountRequests != sentUnmountRequests)
        {
            sentUnmountRequests = context.UnmountRequests;
            await handle.UpdateAsync(Options(context, keepMounted));
        }
    }

    private TransitionsOptions Options(IDialogRootContext context, bool keepMounted) => new()
    {
        Open = context.Open,
        Mounted = context.Mounted,
        Status = renderedStatus,
        KeepMounted = keepMounted,
        HiddenWhenUnmounted = keepMounted,
        ReportOpenComplete = completed is not null,
        ExternalExit = externalExit,
        UnmountRequests = context.UnmountRequests,
        OpenSelector = OpenSelector,
    };

    private async Task AttachAsync(ElementReference element, IDialogRootContext context, bool keepMounted, Task ready)
    {
        int attachedGeneration = generation;
        if (!ready.IsCompleted)
        {
            await ready;
        }

        if (twin is null)
        {
            twin = new TransitionsTwin(jsRuntime);
            if (statusChanged is not null)
            {
                twin.StatusChanged += change => statusChanged(change.Status);
            }

            if (completed is not null)
            {
                twin.TransitionCompleted += opened => _ = invokeAsync(() => completed(opened));
            }
        }

        // The handover: the element was rendered with these values; the module owns them from this attach on.
        frozen = true;
        sentUnmountRequests = context.UnmountRequests;
        TwinHandle<TransitionsOptions> attached;
        try
        {
            attached = await twin.AttachAsync(element, Options(context, keepMounted));
        }
        catch (JSException)
        {
            // Issue 134: the element left the document while the module loaded (closed during a cold attach); dropped, and
            // a later render that shows it attaches again.
            attaching = null;
            frozen = false;
            return;
        }

        if (disposed || attachedGeneration != generation)
        {
            await attached.DisposeAsync();
            return;
        }

        handle = attached;
        attaching = null;
        attachedSource.TrySetResult();
    }

    private async Task DetachAsync()
    {
        generation++;
        attaching = null;
        frozen = false;
        if (handle is { } released)
        {
            handle = null;
            await released.DisposeAsync();
        }
    }
}
