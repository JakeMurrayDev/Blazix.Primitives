using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ADR 04 Amendments 2026-10-27 (decision 3: the token form of attach and the per-popup session; the verdict Prevent at
// escape; chosen by the user 2026-10-27); design point 8 of M4 track 3 (Floating/, settled by the orchestrator
// 2026-10-27; the CompositeRootBehavior precedent). A popup root renders no element: it draws a session token, never
// renders it, cascades it to its parts, and attaches the session handle by token. The Escape chain (FLT-KEY-01,
// FLT-EVT-05; ADR 01 relaxation row "EVD-EVT-11, EVD-DIS-01, FLT-KEY-01"): on WebAssembly the module asks `escape` inside
// the keydown and the decider runs the store's request synchronously, mapping IsCanceled and IsPropagationAllowed to
// the four verdicts; on Server the module applies the fallback to the innermost popup and reports the close.
/// <summary>
/// The session of one popup: the token its parts join, the root's handle on the <c>interactions</c> module, the
/// dismissal configuration and the routing of the module's requests into the popup's store.
/// </summary>
internal sealed class InteractionsSessionBehavior : IAsyncDisposable
{
    private readonly Func<OpenChangeRequest, Task> requestOpenChange;
    private readonly Func<Exception, Task> dispatchException;
    private readonly InteractionsAttachment attachment;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="InteractionsSessionBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="idGenerator">The source of the session token.</param>
    /// <param name="requestOpenChange">Runs the popup store's open-change sequence for a request.</param>
    /// <param name="dispatchException">Routes an exception of a request that ran in a decision to the part.</param>
    public InteractionsSessionBehavior(
        IJSRuntime jsRuntime,
        PartIdGenerator idGenerator,
        Func<OpenChangeRequest, Task> requestOpenChange,
        Func<Exception, Task> dispatchException)
    {
        this.requestOpenChange = requestOpenChange;
        this.dispatchException = dispatchException;
        Token = idGenerator.Next();
        InteractionsTwin twin = new(jsRuntime);
        twin.RequestReported += OnRequest;
        twin.EscapeDecider = DecideEscape;
        attachment = new InteractionsAttachment(twin);
    }

    /// <summary>Gets the session token the popup's parts join; it is never rendered.</summary>
    /// <value>A token drawn once for the popup's lifetime.</value>
    public string Token { get; }

    /// <summary>Gets or sets a value indicating whether the popup is open.</summary>
    /// <value>The open state the popup committed.</value>
    public bool Open { get; set; }

    /// <summary>Gets or sets the id of the trigger the popup is anchored to.</summary>
    /// <value>The trigger's id, or <see langword="null"/> while no trigger is active.</value>
    public string? ActiveTriggerId { get; set; }

    /// <summary>Gets or sets the session token of the enclosing popup.</summary>
    /// <value>The token the enclosing popup cascades, or <see langword="null"/> at the top level.</value>
    public string? ParentToken { get; set; }

    /// <summary>Gets or sets the id of the popup element.</summary>
    /// <value>The id, or <see langword="null"/>.</value>
    public string? FloatingId { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup is in its closing transition.</summary>
    /// <value><see langword="true"/> while the exit animation runs.</value>
    public bool Ending { get; set; }

    /// <summary>Gets or sets the dismissal configuration.</summary>
    /// <value>The configuration, or <see langword="null"/> when nothing dismisses the popup.</value>
    public InteractionsDismissOptions? Dismiss { get; set; }

    /// <summary>Gets the number of option records sent to the module since the attach, the attach's included.</summary>
    /// <value>The count, which an unchanged render does not move.</value>
    internal int Sends => attachment.Sends;

    /// <summary>Gets a task that completes when the handle is attached.</summary>
    /// <value>A task that completes after the first interactive render has attached the handle.</value>
    internal Task TwinAttached => attachment.Attached;

    /// <inheritdoc />
    /// <remarks>Releases the session handle and the twin. Idempotent.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await attachment.DisposeAsync();
    }

    /// <summary>Attaches the session handle after the root's first render, then sends the options that changed.</summary>
    /// <returns>A task that completes when the module has the options.</returns>
    public Task AfterRenderAsync() => attachment.SyncAsync(new InteractionsTarget(null, null, Token), Unchanged, BuildOptions);

    private static DecisionPolicy Verdict(OpenChangeEventDetails details)
    {
        if (details.IsCanceled)
        {
            return details.IsPropagationAllowed ? DecisionPolicy.Run : DecisionPolicy.Stop;
        }

        return details.IsPropagationAllowed ? DecisionPolicy.Prevent : DecisionPolicy.PreventAndStop;
    }

    private bool Unchanged(InteractionsOptions sent) =>
        sent.Session is { } session
        && session.Open == Open
        && session.Ending == Ending
        && string.Equals(session.ActiveTriggerId, ActiveTriggerId, StringComparison.Ordinal)
        && string.Equals(session.ParentToken, ParentToken, StringComparison.Ordinal)
        && string.Equals(session.FloatingId, FloatingId, StringComparison.Ordinal)
        && Equals(session.Dismiss, Dismiss);

    private InteractionsOptions BuildOptions() => new()
    {
        SessionToken = Token,
        Session = new InteractionsSessionOptions
        {
            Open = Open,
            ActiveTriggerId = ActiveTriggerId,
            ParentToken = ParentToken,
            FloatingId = FloatingId,
            Ending = Ending,
            Dismiss = Dismiss,
        },
    };

    private void OnRequest(InteractionsRequest request)
    {
        _ = Observe(requestOpenChange(InteractionsRequests.From(request)));
    }

    // WebAssembly only, synchronously inside the keydown: only a consumer callback that completes synchronously can cancel
    // the close or allow propagation (ADR 04 §3).
    private DecisionPolicy DecideEscape(EventSnapshot snapshot)
    {
        OpenChangeEventDetails details = new(EventReason.EscapeKey, snapshot);
        Task request;
        try
        {
            request = requestOpenChange(new OpenChangeRequest(false, details, false, null));
        }
        catch (Exception exception)
        {
            _ = dispatchException(exception);
            return DecisionPolicy.PreventAndStop;
        }

        _ = Observe(request);
        return Verdict(details);
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
}

/// <summary>Maps the <c>interactions</c> module's request payloads onto the requests a popup store receives.</summary>
internal static class InteractionsRequests
{
    /// <summary>Maps a request the module reported.</summary>
    /// <param name="request">The payload.</param>
    /// <returns>The request, with the reason parsed and the trigger named by id.</returns>
    public static OpenChangeRequest From(InteractionsRequest request) => new(
        request.Open,
        new OpenChangeEventDetails(InteractionsLiterals.ParseReason(request.Reason), request.Event, Trigger(request.TriggerId)),
        request.Instant,
        null);

    /// <summary>Maps a press the module handed over or reported.</summary>
    /// <param name="open">The open state the press computed.</param>
    /// <param name="reason">The reason's literal.</param>
    /// <param name="triggerId">The pressed trigger's id.</param>
    /// <param name="interactionType">How the press happened.</param>
    /// <param name="snapshot">The click's snapshot.</param>
    /// <returns>The request.</returns>
    public static OpenChangeRequest FromPress(bool open, string reason, string? triggerId, string interactionType, EventSnapshot snapshot) => new(
        open,
        new OpenChangeEventDetails(InteractionsLiterals.ParseReason(reason), snapshot, Trigger(triggerId)),
        false,
        interactionType);

    private static EventTrigger? Trigger(string? id) => id is null ? null : new EventTrigger(id, null);
}
