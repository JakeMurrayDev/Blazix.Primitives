using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// What a portal gives the parts inside it (FloatingPortal.tsx's PortalContext), cascaded fixed (ADR 16 rule 9):
// - the portal node, which a nested portal resolves its default container to (FLT-API-13);
// - the move as a task (design point 3 of M4 track 1, settled by the orchestrator 2026-10-27; ADR 04 Amendments
//   2026-10-27): a portaled part's handle awaits it before its first attach, since until the module has moved the node it
//   is `display: none` in place, where no animation runs, nothing measures and nothing takes focus; the Toolbar's
//   first-attach precedent (TLB-STA-04);
// - the focus manager's publication slot (FLT-FOC-18; tracks 4, 5 and 8 publish).
/// <summary>Provides a portal's node, its move and its focus publication to the parts rendered inside it.</summary>
internal sealed class PopupPortalContext
{
    private readonly List<Action> listeners = [];
    private TaskCompletionSource moved = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PortalFocusPublication? publication;

    /// <summary>Gets the portal node once rendered.</summary>
    public ElementReference? Node { get; internal set; }

    /// <summary>Gets the id the portal node renders.</summary>
    public string? NodeId { get; internal set; }

    /// <summary>Gets a task that completes once the module has moved the portal node into its container.</summary>
    public Task Moved => moved.Task;

    /// <summary>Gets the focus manager's publication, or <see langword="null"/> while none is published.</summary>
    public PortalFocusPublication? Publication => publication;

    /// <summary>Gets the task a part inside <paramref name="context"/> awaits before its first attach.</summary>
    /// <param name="context">The enclosing portal's context, or <see langword="null"/> outside any portal.</param>
    /// <returns>The portal's move, or a completed task outside any portal.</returns>
    public static Task WhenMoved(PopupPortalContext? context) => context?.Moved ?? Task.CompletedTask;

    /// <summary>Publishes the focus manager's state, or withdraws it with <see langword="null"/>.</summary>
    /// <param name="next">The publication.</param>
    public void Publish(PortalFocusPublication? next)
    {
        if (Equals(publication, next))
        {
            return;
        }

        publication = next;
        foreach (Action listener in listeners.ToArray())
        {
            listener();
        }
    }

    /// <summary>Adds a listener called when the publication changes.</summary>
    /// <param name="listener">The listener.</param>
    /// <returns>The subscription; disposing it removes the listener.</returns>
    public IDisposable OnPublicationChanged(Action listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        listeners.Add(listener);
        return new Subscription(this, listener);
    }

    /// <summary>Completes <see cref="Moved"/>.</summary>
    internal void CompleteMove() => moved.TrySetResult();

    /// <summary>Starts a new move for a node rendered again after the portal rendered none.</summary>
    internal void ResetMove()
    {
        if (moved.Task.IsCompleted)
        {
            moved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<PopupPortalContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<PopupPortalContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<PopupPortalContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<PopupPortalContext>.ChildContent), content);
        builder.CloseComponent();
    };

    private sealed class Subscription(PopupPortalContext context, Action listener) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => context.listeners.Remove(listener);
    }
}
