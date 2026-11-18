using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// toast/root/ToastRootContext.ts:16-24 (TST-EDGE-02): what Content, Title, Description, Action and Close read from their
// Root. They are non-generic (ADR 08 Amendments 2026-11-18), so the Root, which knows TData, publishes a non-generic view
// of its toast here, from its own selections, and each part subscribes to the aspects it renders, so that a hover
// re-renders only the parts whose expanded flag changed (the render-count rule of the standing rules). Title and
// Description publish their ids through the two label slots; the newest wins and a withdrawal clears only its own id
// (TST-ARIA-04, 05, 10).
internal sealed class ToastRootContext
{
    // TST-EDGE-02, with the port's prefix and code quoting.
    internal const string MissingMessage = "Blazix: `ToastRootContext` is missing. Toast parts must be used within `<ToastRoot>`.";

    private readonly Action<string> close;
    private readonly List<Subscription> subscriptions = [];
    private ToastRootAspects pending;
    private int version;

    public ToastRootContext(Action<string> close)
    {
        this.close = close;
    }

    public LabelIdSlot TitleId { get; } = new();

    public LabelIdSlot DescriptionId { get; } = new();

    public string ToastId { get; private set; } = string.Empty;

    public string? Type { get; private set; }

    public string? Title { get; private set; }

    public RenderFragment? TitleContent { get; private set; }

    public string? Description { get; private set; }

    public RenderFragment? DescriptionContent { get; private set; }

    public IReadOnlyDictionary<string, object>? ActionAttributes { get; private set; }

    public RenderFragment? ActionContent { get; private set; }

    public bool Expanded { get; private set; }

    // TST-DOM-15: the visible index is greater than 0; an ending toast (-1) is never behind.
    public bool Behind { get; private set; }

    public static ToastRootContext Require(ToastRootContext? context) => context ?? throw new InvalidOperationException(MissingMessage);

    // TST-PTR-26, TST-KEY-05: the toast's close, by id (TST-EDGE-10).
    public void Close() => close(ToastId);

    public ToastRootSubscription Subscribe(ToastRootAspects aspects, Action changed)
    {
        Subscription subscription = new(this, aspects, changed) { RenderedVersion = version };
        subscriptions.Add(subscription);
        return subscription;
    }

    // From the Root's OnAfterRender: notifies each part that renders a changed aspect and has not rendered since.
    public void Flush()
    {
        if (pending == ToastRootAspects.None)
        {
            return;
        }

        ToastRootAspects changed = pending;
        pending = ToastRootAspects.None;
        for (int i = 0; i < subscriptions.Count; i++)
        {
            Subscription subscription = subscriptions[i];
            if ((subscription.Aspects & changed) != 0 && subscription.RenderedVersion < version)
            {
                subscription.Changed();
            }
        }
    }

    // Called by the Root with its current selections; notifies each part whose aspects changed.
    public void Publish<TData>(ToastObject<TData> toast, bool expanded, int visibleIndex)
    {
        ToastRootAspects changed = ToastRootAspects.None;
        if (!string.Equals(ToastId, toast.Id, StringComparison.Ordinal))
        {
            ToastId = toast.Id;
            changed |= ToastRootAspects.Id;
        }

        if (!string.Equals(Type, toast.Type, StringComparison.Ordinal))
        {
            Type = toast.Type;
            changed |= ToastRootAspects.Type;
        }

        if (!string.Equals(Title, toast.Title, StringComparison.Ordinal) || !ReferenceEquals(TitleContent, toast.TitleContent))
        {
            Title = toast.Title;
            TitleContent = toast.TitleContent;
            changed |= ToastRootAspects.Title;
        }

        if (!string.Equals(Description, toast.Description, StringComparison.Ordinal) || !ReferenceEquals(DescriptionContent, toast.DescriptionContent))
        {
            Description = toast.Description;
            DescriptionContent = toast.DescriptionContent;
            changed |= ToastRootAspects.Description;
        }

        if (!ReferenceEquals(ActionAttributes, toast.ActionAttributes) || !ReferenceEquals(ActionContent, toast.ActionContent))
        {
            ActionAttributes = toast.ActionAttributes;
            ActionContent = toast.ActionContent;
            changed |= ToastRootAspects.Action;
        }

        if (Expanded != expanded)
        {
            Expanded = expanded;
            changed |= ToastRootAspects.Expanded;
        }

        bool behind = visibleIndex > 0;
        if (Behind != behind)
        {
            Behind = behind;
            changed |= ToastRootAspects.Behind;
        }

        if (changed != ToastRootAspects.None)
        {
            version++;
            pending |= changed;
        }
    }

    private sealed class Subscription(ToastRootContext context, ToastRootAspects aspects, Action changed) : ToastRootSubscription
    {
        public ToastRootAspects Aspects { get; } = aspects;

        public Action Changed { get; } = changed;

        public int RenderedVersion { get; set; }

        /// <inheritdoc />
        public override void Dispose() => context.subscriptions.Remove(this);

        /// <inheritdoc />
        public override void Rendered() => RenderedVersion = context.version;
    }
}

// A part's subscription to its Root's aspects; the part calls Rendered from each render.
internal abstract class ToastRootSubscription : IDisposable
{
    /// <inheritdoc />
    public abstract void Dispose();

    public abstract void Rendered();
}

// What a part under a Root renders from it.
[Flags]
internal enum ToastRootAspects
{
    None = 0,
    Id = 1 << 0,
    Type = 1 << 1,
    Title = 1 << 2,
    Description = 1 << 3,
    Action = 1 << 4,
    Expanded = 1 << 5,
    Behind = 1 << 6,
}
