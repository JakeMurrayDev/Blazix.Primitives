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
// - on Server the module reports the press, whose message precedes Blazor's own dispatch of the click: the report runs
//   the consumer's onclick and then the request, and the part's onclick skips the click it already ran. Whichever of
//   the two arrives first runs the handler; the other reads its verdict.
// Every other request the trigger's handle makes (hover, focus, reference press) arrives as a report.
/// <summary>
/// One trigger's handle on the <c>interactions</c> module: its options, the gate of the consumer's <c>onclick</c>, the press
/// decision and the routing of its requests into the popup's store.
/// </summary>
internal sealed class InteractionsTriggerBehavior : IAsyncDisposable
{
    private readonly Func<OpenChangeRequest, Task> requestOpenChange;
    private readonly Func<Exception, Task> dispatchException;
    private readonly EventCallback onClick;
    private readonly InteractionsAttachment attachment;
    private AttributeValue consumerClick;
    private DecidedClick? decidedClick;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="InteractionsTriggerBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="requestOpenChange">Runs the popup store's open-change sequence for a request.</param>
    /// <param name="dispatchException">Routes an exception of a consumer handler that ran in a decision to the part.</param>
    /// <param name="onClick">The part's own <c>onclick</c> handler, which calls <see cref="HandleClickAsync"/>.</param>
    public InteractionsTriggerBehavior(
        IJSRuntime jsRuntime,
        Func<OpenChangeRequest, Task> requestOpenChange,
        Func<Exception, Task> dispatchException,
        EventCallback onClick)
    {
        this.requestOpenChange = requestOpenChange;
        this.dispatchException = dispatchException;
        this.onClick = onClick;
        InteractionsTwin twin = new(jsRuntime);
        twin.RequestReported += OnRequest;
        twin.PressReported += OnPress;
        twin.PressDecider = DecidePress;
        attachment = new InteractionsAttachment(twin);
    }

    /// <summary>Gets or sets the session token of the trigger's popup.</summary>
    /// <value>The token the popup root cascades.</value>
    public string? SessionToken { get; set; }

    /// <summary>Gets or sets the trigger's options.</summary>
    /// <value>The options the part builds each render; compared by value, so an equal record sends nothing.</value>
    public InteractionsTriggerOptions Options { get; set; } = new();

    /// <summary>Gets the number of option records sent to the module since the attach, the attach's included.</summary>
    /// <value>The count, which an unchanged render does not move.</value>
    internal int Sends => attachment.Sends;

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

    /// <summary>Reads the consumer's <c>onclick</c> handler from the unmatched attributes.</summary>
    /// <param name="additionalAttributes">The part's unmatched attributes.</param>
    public void SetConsumerAttributes(IReadOnlyDictionary<string, object>? additionalAttributes)
    {
        consumerClick = default;
        if (additionalAttributes is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object> pair in additionalAttributes)
        {
            if (!IsClick(pair.Key))
            {
                continue;
            }

            AttributeValue value = AttributeValue.FromObject(pair.Value);
            if (MergeProps.IsHandler(pair.Key, value))
            {
                consumerClick = value;
            }
        }
    }

    /// <summary>Determines whether the consumer's handler for an event is rendered on the element.</summary>
    /// <param name="eventName">The event's attribute name in lowercase.</param>
    /// <returns><see langword="false"/> for <c>onclick</c> while a press toggles the popup; otherwise <see langword="true"/>.</returns>
    public bool ForwardsConsumerHandler(string eventName) => !(Options.Click is not null && IsClick(eventName));

    /// <summary>Adds the part's own <c>onclick</c> handler while it runs the consumer's.</summary>
    /// <param name="attributes">The part-supplied attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        if (Options.Click is not null && consumerClick.IsHandler)
        {
            attributes.Set("onclick", onClick);
        }
    }

    /// <summary>Runs the consumer's <c>onclick</c> handler from the part's own, unless the press already ran it.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleClickAsync(MouseEventArgs e)
    {
        ClickKey current = DecidedClick.From(e);
        if (decidedClick is { FromDispatch: false } decided && decided.Click == current)
        {
            decidedClick = null;
            return Task.CompletedTask;
        }

        if (!consumerClick.IsHandler)
        {
            return Task.CompletedTask;
        }

        // Blazor's dispatch arrived before the module's report (Server): the report reads this verdict.
        decidedClick = new DecidedClick(current, e.PartHandlerPrevented(), FromDispatch: true);
        Task handler = consumerClick.InvokeAsync(e);
        decidedClick = decidedClick.Value with { Vetoed = e.PartHandlerPrevented() };
        return handler;
    }

    /// <summary>Attaches the trigger's handle after the first render that showed its element, then sends the options that changed.</summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The element, or <see langword="null"/> under a substitute callback.</param>
    /// <param name="id">The id the part rendered on its own element.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    public Task AfterRenderAsync(bool enabled, ElementReference? element, string? id)
    {
        if (!enabled)
        {
            return attachment.DetachAsync();
        }

        return attachment.SyncAsync(new InteractionsTarget(element, element is null ? id : null, null), Unchanged, BuildOptions);
    }

    private static bool IsClick(string name) => string.Equals(name, "onclick", StringComparison.OrdinalIgnoreCase);

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
        string.Equals(sent.SessionToken, SessionToken, StringComparison.Ordinal) && Equals(sent.Trigger, Options);

    private InteractionsOptions BuildOptions() => new() { SessionToken = SessionToken, Trigger = Options };

    // Runs the consumer's onclick for the click a press is about, once, and returns whether it vetoed the press.
    private bool RunConsumerClick(EventSnapshot snapshot)
    {
        if (!consumerClick.IsHandler)
        {
            return false;
        }

        MouseEventArgs args = ToArgs(snapshot);
        ClickKey key = DecidedClick.From(args);
        if (decidedClick is { FromDispatch: true } dispatched && dispatched.Click == key)
        {
            decidedClick = null;
            return dispatched.Vetoed;
        }

        decidedClick = new DecidedClick(key, false, FromDispatch: false);
        try
        {
            Task handler = consumerClick.InvokeAsync(args);
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

    // WebAssembly only, synchronously inside the click.
    private DecisionPolicy DecidePress(EventSnapshot snapshot)
    {
        if (attachment.Twin.PendingPress is not { } press)
        {
            return DecisionPolicy.Run;
        }

        if (RunConsumerClick(snapshot))
        {
            return DecisionPolicy.Stop;
        }

        _ = Observe(requestOpenChange(InteractionsRequests.FromPress(press.Open, press.Reason, press.TriggerId, press.InteractionType, snapshot)));
        return DecisionPolicy.Run;
    }

    private void OnPress(InteractionsPress press)
    {
        if (RunConsumerClick(press.Event))
        {
            return;
        }

        _ = Observe(requestOpenChange(InteractionsRequests.FromPress(press.Open, press.Reason, press.TriggerId, press.InteractionType, press.Event)));
    }

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

    /// <summary>The click a handler already ran for: its key, the verdict and which path ran it.</summary>
    private readonly record struct DecidedClick(ClickKey Click, bool Vetoed, bool FromDispatch)
    {
        public static ClickKey From(MouseEventArgs e) => new(e.Detail, e.AltKey, e.CtrlKey, e.MetaKey, e.ShiftKey);
    }

    /// <summary>What matches a decided click against the other path's arrival of the same click.</summary>
    private readonly record struct ClickKey(long Detail, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey);
}
