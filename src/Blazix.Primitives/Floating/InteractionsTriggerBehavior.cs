using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ADR 04 and ADR 01 Amendments 2026-10-27 (the decision point press; chosen by the user 2026-10-27); design point 8 of
// M4 track 3 (Floating/, settled by the orchestrator 2026-10-27). The module decides the next open value on the
// trigger (FLT-ALG-16) and asks `press`; the consumer's onclick must run first and may veto (FLT-PTR-03; ADR 01 row
// "C#-origin consumer vetoes"), so the part declines it (ForwardsConsumerHandler, ADR 02 Amendments 2026-10-21; the
// ButtonBehavior model):
// - on WebAssembly the press decider runs it synchronously inside the click, and the request unless it called
//   PreventPartHandler(); Blazor then dispatches the same click to the part's own onclick, which skips it (a one-shot
//   marker matched by click count and modifiers);
// - on Server the module reports the press, whose message precedes Blazor's own dispatch of the click on one circuit
//   (ADR-04/1): the report runs the consumer's onclick and then the request, and the part's onclick skips the click it
//   already ran. A dispatch with no press before it is a click the module did not turn into a press: it runs the
//   handler and leaves nothing behind.
// A trigger that toggles from the mousedown (Click.Event mousedown or mousedown-only; Menu's trigger, M5 track 2) declines
// the consumer's onmousedown the same way, and a press from the mousedown runs it instead of the onclick (Base UI merges
// the consumer's onMouseDown before useClick's, and preventBaseUIHandler() there skips the press). A press deferred to a
// frame or a touch delay (FLT-PTR-02, FLT-PTR-04) fires after Blazor dispatched its event on both modes, so the dispatch
// runs the handler and keeps its verdict, and the press, which says it was deferred, takes that verdict instead of
// running the handler again.
// Every other request the trigger's handle makes (hover, focus, reference press) arrives as a report.
/// <summary>
/// One trigger's handle on the <c>interactions</c> module: its options, the gates of the consumer's <c>onclick</c> and
/// <c>onmousedown</c>, the press decision and the routing of its requests into the popup's store.
/// </summary>
internal sealed class InteractionsTriggerBehavior : IAsyncDisposable
{
    private readonly IJSRuntime jsRuntime;
    private readonly Func<OpenChangeRequest, Task> requestOpenChange;
    private readonly Func<Exception, Task> dispatchException;
    private readonly EventCallback onClick;
    private readonly EventCallback onMouseDown;
    private readonly InteractionsAttachment attachment;
    private readonly ConsumerGate clickGate = new();
    private readonly ConsumerGate mouseDownGate = new();
    private string? attachedToken;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="InteractionsTriggerBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="requestOpenChange">Runs the popup store's open-change sequence for a request.</param>
    /// <param name="dispatchException">Routes an exception of a consumer handler that ran in a decision to the part.</param>
    /// <param name="onClick">The part's own <c>onclick</c> handler, which calls <see cref="HandleClickAsync"/>.</param>
    /// <param name="onMouseDown">
    /// The part's own <c>onmousedown</c> handler, which calls <see cref="HandleMouseDownAsync"/>; needed only by a trigger
    /// whose press can toggle from the <c>mousedown</c>.
    /// </param>
    public InteractionsTriggerBehavior(
        IJSRuntime jsRuntime,
        Func<OpenChangeRequest, Task> requestOpenChange,
        Func<Exception, Task> dispatchException,
        EventCallback onClick,
        EventCallback onMouseDown = default)
    {
        this.jsRuntime = jsRuntime;
        this.requestOpenChange = requestOpenChange;
        this.dispatchException = dispatchException;
        this.onClick = onClick;
        this.onMouseDown = onMouseDown;
        InteractionsTwin twin = new(jsRuntime);
        twin.RequestReported += OnRequest;
        twin.PressReported += OnPress;
        twin.PressDecider = DecidePress;
        attachment = new InteractionsAttachment(twin);
    }

    // Only a part that supplies its own onmousedown declines the consumer's; another forwards it unchanged.
    private bool TogglesFromMouseDown => Options.Click?.Event is "mousedown" or "mousedown-only" && onMouseDown.HasDelegate;

    /// <summary>Gets or sets the session token of the trigger's popup.</summary>
    /// <value>The token the popup root cascades.</value>
    public string? SessionToken { get; set; }

    /// <summary>Gets or sets the trigger's options.</summary>
    /// <value>The options the part builds each render; compared by value, so an equal record sends nothing.</value>
    public InteractionsTriggerOptions Options { get; set; } = new();

    /// <summary>Gets the number of option records sent to the module since the attach, the attach's included.</summary>
    /// <value>The count, which an unchanged render does not move.</value>
    internal int Sends => attachment.Sends;

    /// <summary>Gets a task that completes when the handle is attached.</summary>
    /// <value>A task that completes after the first interactive render has attached the handle.</value>
    internal Task TwinAttached => attachment.Attached;

    /// <inheritdoc />
    /// <remarks>Releases the trigger's handle and the twin. Idempotent.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await attachment.DisposeAsync();
    }

    /// <summary>Reads the consumer's <c>onclick</c> and <c>onmousedown</c> handlers from the unmatched attributes.</summary>
    /// <param name="additionalAttributes">The part's unmatched attributes.</param>
    public void SetConsumerAttributes(IReadOnlyDictionary<string, object>? additionalAttributes)
    {
        clickGate.Handler = default;
        mouseDownGate.Handler = default;
        if (additionalAttributes is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object> pair in additionalAttributes)
        {
            ConsumerGate? gate = IsClick(pair.Key) ? clickGate : IsMouseDown(pair.Key) ? mouseDownGate : null;
            if (gate is null)
            {
                continue;
            }

            AttributeValue value = AttributeValue.FromObject(pair.Value);
            if (MergeProps.IsHandler(pair.Key, value))
            {
                gate.Handler = value;
            }
        }
    }

    /// <summary>Determines whether the consumer's handler for an event is rendered on the element.</summary>
    /// <param name="eventName">The event's attribute name in lowercase.</param>
    /// <returns>
    /// <see langword="false"/> for <c>onclick</c> while a press toggles the popup, and for <c>onmousedown</c> while the press
    /// toggles from the mousedown and the part supplied its own handler; otherwise <see langword="true"/>.
    /// </returns>
    public bool ForwardsConsumerHandler(string eventName) =>
        !(Options.Click is not null && (IsClick(eventName) || (TogglesFromMouseDown && IsMouseDown(eventName))));

    /// <summary>Adds the part's own <c>onclick</c> and <c>onmousedown</c> handlers while they run the consumer's.</summary>
    /// <param name="attributes">The part-supplied attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        if (Options.Click is not null && clickGate.Handler.IsHandler)
        {
            attributes.Set("onclick", onClick);
        }

        if (Options.Click is not null && TogglesFromMouseDown && mouseDownGate.Handler.IsHandler)
        {
            attributes.Set("onmousedown", onMouseDown);
        }
    }

    /// <summary>Runs the consumer's <c>onclick</c> handler from the part's own, unless the press already ran it.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleClickAsync(MouseEventArgs e) => clickGate.RunFromDispatch(e);

    /// <summary>Runs the consumer's <c>onmousedown</c> handler from the part's own, unless the press already ran it.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleMouseDownAsync(MouseEventArgs e) => mouseDownGate.RunFromDispatch(e);

    // The module keys a trigger handle by the session it attached under (a fidelity correction of track 3's behaviour,
    // found by track 5 on detached triggers; settled by the orchestrator 2026-10-28): a trigger whose store has no session
    // yet (a detached trigger in its handle's fallback store) attaches once a root's store gives it a token, and attaches
    // again under another root's token. A token missing for a batch (a recreated handle before its root attaches) keeps
    // the live handle and its token, so a press right after still reaches the session.
    /// <summary>
    /// Attaches the trigger's handle after the first render that showed its element once the trigger has a session token,
    /// attaches it again under a different token, then sends the options that changed.
    /// </summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The element, or <see langword="null"/> under a substitute callback.</param>
    /// <param name="id">The id the part rendered on its own element.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public async Task AfterRenderAsync(bool enabled, ElementReference? element, string? id)
    {
        if (SessionToken is { } token && attachedToken is not null && !string.Equals(attachedToken, token, StringComparison.Ordinal))
        {
            await attachment.DetachAsync();
            attachedToken = null;
        }

        attachedToken ??= SessionToken;
        if (!enabled || attachedToken is null)
        {
            await attachment.DetachAsync();
            return;
        }

        await attachment.SyncAsync(new InteractionsTarget(element, element is null ? id : null, null), Unchanged, BuildOptions);
    }

    private static bool IsClick(string name) => string.Equals(name, "onclick", StringComparison.OrdinalIgnoreCase);

    private static bool IsMouseDown(string name) => string.Equals(name, "onmousedown", StringComparison.OrdinalIgnoreCase);

    private static MouseEventArgs ToArgs(EventSnapshot snapshot) => new()
    {
        Type = snapshot.Type,
        Detail = snapshot.Detail,
        Buttons = snapshot.Buttons,
        AltKey = snapshot.AltKey,
        CtrlKey = snapshot.CtrlKey,
        MetaKey = snapshot.MetaKey,
        ShiftKey = snapshot.ShiftKey,
    };

    private bool Unchanged(InteractionsOptions sent) =>
        string.Equals(sent.SessionToken, attachedToken, StringComparison.Ordinal) && Equals(sent.Trigger, Options);

    private InteractionsOptions BuildOptions() => new() { SessionToken = attachedToken, Trigger = Options };

    // Runs the consumer's handler for the event a press is about (its onmousedown for a mousedown, its onclick otherwise)
    // once, and returns whether it vetoed the press. A deferred press takes the verdict of Blazor's dispatch of its event.
    private bool RunConsumerHandler(EventSnapshot snapshot, bool deferred)
    {
        ConsumerGate gate = string.Equals(snapshot.Type, "mousedown", StringComparison.Ordinal) ? mouseDownGate : clickGate;
        if (!gate.Handler.IsHandler)
        {
            return false;
        }

        MouseEventArgs args = ToArgs(snapshot);
        if (deferred && gate.TakeDispatchVerdict(args) is { } vetoed)
        {
            return vetoed;
        }

        // A press inside its event precedes Blazor's dispatch of it, which skips the event; a deferred one follows it.
        gate.MarkDecided(args, !deferred);
        try
        {
            Task handler = gate.Handler.InvokeAsync(args);
            if (!handler.IsCompleted)
            {
                _ = Observe(handler);
            }
            else if (handler.IsFaulted)
            {
                _ = dispatchException(handler.Exception!.GetBaseException());
            }
        }
        catch (Exception exception)
        {
            _ = dispatchException(exception);
        }

        return args.PartHandlerPrevented();
    }

    // WebAssembly only, synchronously inside the click, the mousedown, or the frame or timer a deferred press fires from.
    private DecisionPolicy DecidePress(EventSnapshot snapshot)
    {
        if (attachment.Twin.PendingPress is not { } press)
        {
            return DecisionPolicy.Run;
        }

        // ADR 04 Amendments 2026-11-11: the session answers the press, vetoed or not; the press runs synchronously inside.
        DecisionPolicy verdict = DecisionPolicy.Run;
        _ = Observe(InteractionsSessionBehavior.AnswerPressAsync(jsRuntime, attachedToken ?? SessionToken, () =>
        {
            if (RunConsumerHandler(snapshot, press.Deferred))
            {
                verdict = DecisionPolicy.Stop;
                return Task.CompletedTask;
            }

            return requestOpenChange(InteractionsRequests.FromPress(press.Open, press.Reason, press.TriggerId, press.InteractionType, snapshot));
        }));
        return verdict;
    }

    // ADR 04 Amendments 2026-11-11: every press is answered through the session's counter, a vetoed or canceled one too,
    // so the module ends its press-requested open's count as open (and rolls back its optimistic open event, gap 16).
    private void OnPress(InteractionsPress press) =>
        _ = Observe(InteractionsSessionBehavior.AnswerPressAsync(jsRuntime, attachedToken ?? SessionToken, () =>
            RunConsumerHandler(press.Event, press.Deferred)
                ? Task.CompletedTask
                : requestOpenChange(InteractionsRequests.FromPress(press.Open, press.Reason, press.TriggerId, press.InteractionType, press.Event))));

    private void OnRequest(InteractionsRequest request)
    {
        _ = Observe(requestOpenChange(InteractionsRequests.From(request)));
    }

    private async Task Observe(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await dispatchException(exception);
        }
    }

    /// <summary>Builds the key of a click a press already ran the consumer's handler for.</summary>
    private static class DecidedClick
    {
        /// <summary>Returns the key of a click.</summary>
        /// <param name="e">The click's arguments.</param>
        /// <returns>The click count and modifiers, matched against Blazor's dispatch of the same click.</returns>
        public static ClickKey From(MouseEventArgs e) => new(e.Detail, e.AltKey, e.CtrlKey, e.MetaKey, e.ShiftKey);
    }

    /// <summary>
    /// One declined consumer handler, <c>onclick</c> or <c>onmousedown</c>, and what a press and Blazor's dispatch of the
    /// same event tell each other so it runs once per event.
    /// </summary>
    private sealed class ConsumerGate
    {
        private ClickKey? decided;
        private ClickKey? dispatched;
        private bool dispatchVetoed;

        /// <summary>Gets or sets the consumer's handler.</summary>
        /// <value>The handler read from the unmatched attributes, or the default value when the consumer supplied none.</value>
        public AttributeValue Handler { get; set; }

        /// <summary>Runs the handler from Blazor's dispatch, unless a press inside the event already ran it.</summary>
        /// <param name="e">The event arguments.</param>
        /// <returns>The handler's task.</returns>
        public Task RunFromDispatch(MouseEventArgs e)
        {
            ClickKey key = DecidedClick.From(e);
            ClickKey? marked = decided;
            decided = null;
            if (marked == key || !Handler.IsHandler)
            {
                return Task.CompletedTask;
            }

            Task handler = Handler.InvokeAsync(e);
            dispatched = key;
            dispatchVetoed = e.PartHandlerPrevented();
            return handler;
        }

        /// <summary>Takes the verdict of the dispatch that ran the handler for a deferred press's event.</summary>
        /// <param name="args">The press's event arguments.</param>
        /// <returns>Whether the dispatch's run vetoed the press, or <see langword="null"/> when no dispatch ran it.</returns>
        public bool? TakeDispatchVerdict(MouseEventArgs args)
        {
            if (dispatched != DecidedClick.From(args))
            {
                return null;
            }

            dispatched = null;
            return dispatchVetoed;
        }

        /// <summary>Marks the event a press runs the handler for, so Blazor's dispatch of it skips the handler.</summary>
        /// <param name="args">The press's event arguments.</param>
        /// <param name="dispatchFollows">Whether Blazor's dispatch of the event is still to come.</param>
        public void MarkDecided(MouseEventArgs args, bool dispatchFollows) => decided = dispatchFollows ? DecidedClick.From(args) : null;
    }

    /// <summary>What matches a press's click against Blazor's dispatch of the same click.</summary>
    /// <param name="Detail">The click count.</param>
    /// <param name="AltKey">Whether Alt was held.</param>
    /// <param name="CtrlKey">Whether Control was held.</param>
    /// <param name="MetaKey">Whether Meta was held.</param>
    /// <param name="ShiftKey">Whether Shift was held.</param>
    private readonly record struct ClickKey(long Detail, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey);
}
