using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Design points 2 and 8 of M4 track 3 (the floating role, pending the user; Floating/, settled by the orchestrator
// 2026-10-27): the floating element (a Positioner, or Dialog's Popup) carries the inside-press marks, the floating keydown
// and the hover leave (FLT-DIS-04/12, FLT-KEY-01, FLT-PTR-23/24), and publishes the tree node's context (FLT-STR-08). The
// Positioner carries a second handle (positioning's), so the attach order of the two is pinned by an L4 title.
/// <summary>The floating element's handle on the <c>interactions</c> module: its tree node, its hover close and its requests.</summary>
internal sealed class InteractionsFloatingBehavior : IAsyncDisposable
{
    private readonly Func<OpenChangeRequest, Task> requestOpenChange;
    private readonly Func<Exception, Task> dispatchException;
    private readonly InteractionsAttachment attachment;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="InteractionsFloatingBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="requestOpenChange">Runs the popup store's open-change sequence for a request.</param>
    /// <param name="dispatchException">Routes an exception of a request to the part.</param>
    public InteractionsFloatingBehavior(IJSRuntime jsRuntime, Func<OpenChangeRequest, Task> requestOpenChange, Func<Exception, Task> dispatchException)
    {
        this.requestOpenChange = requestOpenChange;
        this.dispatchException = dispatchException;
        InteractionsTwin twin = new(jsRuntime);
        twin.RequestReported += OnRequest;
        attachment = new InteractionsAttachment(twin);
    }

    /// <summary>Gets or sets the session token of the popup.</summary>
    /// <value>The token the popup root cascades.</value>
    public string? SessionToken { get; set; }

    /// <summary>Gets or sets the floating element's options.</summary>
    /// <value>The options the part builds each render; compared by value, so an equal record sends nothing.</value>
    public InteractionsFloatingOptions Options { get; set; } = new();

    /// <summary>Gets the number of option records sent to the module since the attach, the attach's included.</summary>
    /// <value>The count, which an unchanged render does not move.</value>
    internal int Sends => attachment.Sends;

    /// <inheritdoc />
    /// <remarks>Releases the floating element's handle and the twin. Idempotent.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await attachment.DisposeAsync();
    }

    /// <summary>Attaches the handle after the first render that showed the floating element, then sends the options that changed.</summary>
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

    private bool Unchanged(InteractionsOptions sent) =>
        string.Equals(sent.SessionToken, SessionToken, StringComparison.Ordinal) && Equals(sent.Floating, Options);

    private InteractionsOptions BuildOptions() => new() { SessionToken = SessionToken, Floating = Options };

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
}
