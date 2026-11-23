using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// SCR §2, §4.2-§4.4 (scroll-area/root/ScrollAreaRootContext.ts). A plain listener list cascaded fixed (ADR 16 rule 9; the
// Tabs precedent): the `scroll-area` module measures, and its reports reach C# as the overflow state, which C# projects, and
// the scrolling and hovering flags, which the module writes and C# mirrors (RND-STA-01). The Root listens first, so a change
// the Root renders re-renders the subtree in the same render batch and the other parts then find nothing left to render.
// Thumb size, corner size, the drag record, the touch modality and the programmatic-scroll marker stay in JavaScript.
/// <summary>The state a scroll area root shares with every part in its subtree.</summary>
internal sealed class ScrollAreaRootContext
{
    private readonly List<Action> listeners = [];
    private Action? rootListener;

    /// <summary>Initializes a new instance of the <see cref="ScrollAreaRootContext"/> class.</summary>
    /// <param name="rootId">The id the root generated, which the viewport and the scrollbars suffix into <c>data-id</c>.</param>
    public ScrollAreaRootContext(string rootId)
    {
        RootId = rootId;
    }

    /// <summary>Gets the function that reads the root's element, which every part's handle receives as a reference.</summary>
    /// <value>Set by the root; returns <see langword="null"/> while the root renders no element.</value>
    public Func<ElementReference?> RootElement { get; init; } = static () => null;

    /// <summary>Gets the id the root generated.</summary>
    /// <value>A <c>base-ui-&lt;n&gt;</c> id, never rendered on the root itself.</value>
    public string RootId { get; }

    /// <summary>Gets the overflow state of the last measurement.</summary>
    /// <value>No overflow until the first report.</value>
    public ScrollAreaOverflow Overflow { get; private set; }

    // SCR-STA-07: `cornerHidden = hidden.x || hidden.y`.
    /// <summary>Gets a value indicating whether the corner is hidden, because at least one axis does not overflow.</summary>
    public bool CornerHidden => !(Overflow.HasOverflowX && Overflow.HasOverflowY);

    // SCR-STA-19: set by the first measurement attempt and never reset (§16 Q7).
    /// <summary>Gets a value indicating whether the browser-side behavior has measured the viewport at least once.</summary>
    public bool Measured { get; private set; }

    // SCR-STA-11: the per-axis flags the module writes as data-scrolling; C#'s mirror.
    /// <summary>Gets a value indicating whether the horizontal axis was scrolled by the user within the last 500 milliseconds.</summary>
    public bool ScrollingX { get; private set; }

    /// <summary>Gets a value indicating whether the vertical axis was scrolled by the user within the last 500 milliseconds.</summary>
    public bool ScrollingY { get; private set; }

    // SCR-STA-12.
    /// <summary>Gets a value indicating whether either axis was scrolled by the user within the last 500 milliseconds.</summary>
    public bool Scrolling => ScrollingX || ScrollingY;

    // SCR-STA-15: the flag the module writes as the scrollbars' data-hovering; C#'s mirror.
    /// <summary>Gets a value indicating whether a pointer other than touch is over the scroll area.</summary>
    public bool Hovering { get; private set; }

    /// <summary>Throws the error of a scroll area part rendered outside a scroll area root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static ScrollAreaRootContext Require(ScrollAreaRootContext? context)
    {
        // SCR-EDGE-01: Base UI's wording with the product prefix and code in backticks (plan §Settled; a dated note on the
        // row), thrown in every build (ADR 02 Amendments 2026-10-20).
        return context ?? throw new InvalidOperationException(
            "Blazix: `ScrollAreaRootContext` is missing. ScrollArea parts must be placed within `<ScrollAreaRoot>`.");
    }

    /// <summary>Sets the root's listener, which runs before every other listener.</summary>
    /// <param name="listener">The listener.</param>
    public void SetRootListener(Action listener) => rootListener = listener;

    /// <summary>Adds a listener that runs after every change of the root's shared state.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    /// <summary>Takes a measurement's overflow state and notifies the parts when it or the measured flag changed.</summary>
    /// <param name="overflow">The overflow state.</param>
    /// <param name="measured">Whether the viewport was measured.</param>
    public void ApplyOverflow(ScrollAreaOverflow overflow, bool measured)
    {
        // The measured flag never reverts (SCR-STA-19).
        bool nextMeasured = Measured || measured;
        if (overflow == Overflow && nextMeasured == Measured)
        {
            return;
        }

        Overflow = overflow;
        Measured = nextMeasured;
        Notify();
    }

    /// <summary>Takes the scrolling flags the browser-side behavior wrote and notifies the parts when either changed.</summary>
    /// <param name="x">The horizontal flag.</param>
    /// <param name="y">The vertical flag.</param>
    public void MirrorScrolling(bool x, bool y)
    {
        if (x == ScrollingX && y == ScrollingY)
        {
            return;
        }

        ScrollingX = x;
        ScrollingY = y;
        Notify();
    }

    /// <summary>Takes the hovering flag the browser-side behavior wrote and notifies the parts when it changed.</summary>
    /// <param name="hovering">The flag.</param>
    public void MirrorHovering(bool hovering)
    {
        if (hovering == Hovering)
        {
            return;
        }

        Hovering = hovering;
        Notify();
    }

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<ScrollAreaRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ScrollAreaRootContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<ScrollAreaRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ScrollAreaRootContext>.ChildContent), content);
        builder.CloseComponent();
    };

    private void Notify()
    {
        rootListener?.Invoke();

        // A listener may remove itself or another (a part disposed by the render it triggers): iterating backwards with a
        // bounds check never skips a listener that is still registered.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }
}

// SCR-STA-06, SCR-STA-09: what a measurement reports, before the first one all false (SCR-STA-08).
/// <summary>The overflow state of a scroll area's viewport.</summary>
/// <param name="HasOverflowX">Whether the content is wider than the viewport.</param>
/// <param name="HasOverflowY">Whether the content is taller than the viewport.</param>
/// <param name="XStart">Whether the viewport is scrolled from the inline start edge by more than its threshold.</param>
/// <param name="XEnd">Whether the viewport is scrolled from the inline end edge by more than its threshold.</param>
/// <param name="YStart">Whether the viewport is scrolled from the top edge by more than its threshold.</param>
/// <param name="YEnd">Whether the viewport is scrolled from the bottom edge by more than its threshold.</param>
internal readonly record struct ScrollAreaOverflow(bool HasOverflowX, bool HasOverflowY, bool XStart, bool XEnd, bool YStart, bool YEnd);

// SCR-EDGE-02 (scroll-area/viewport/ScrollAreaViewportContext.ts): what a Content binds to.
/// <summary>The marker a scroll area viewport cascades to its content.</summary>
internal sealed class ScrollAreaViewportContext
{
    /// <summary>Throws the error of a scroll area content rendered outside a scroll area viewport.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static ScrollAreaViewportContext Require(ScrollAreaViewportContext? context)
    {
        // SCR-EDGE-02: Base UI's wording, which has no "is", with the product prefix and code in backticks.
        return context ?? throw new InvalidOperationException(
            "Blazix: `ScrollAreaViewportContext` missing. ScrollAreaViewport parts must be placed within `<ScrollAreaViewport>`.");
    }

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<ScrollAreaViewportContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ScrollAreaViewportContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<ScrollAreaViewportContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ScrollAreaViewportContext>.ChildContent), content);
        builder.CloseComponent();
    };
}

// SCR-STR-05, SCR-EDGE-03 (scroll-area/scrollbar/ScrollAreaScrollbarContext.ts): the orientation a Thumb takes.
/// <summary>The orientation a scroll area scrollbar cascades to its thumb.</summary>
internal sealed class ScrollAreaScrollbarContext
{
    private Action? listener;

    /// <summary>Gets the scrollbar's orientation.</summary>
    /// <value>The orientation of the scrollbar's last render.</value>
    public Orientation Orientation { get; private set; } = Orientation.Vertical;

    /// <summary>Throws the error of a scroll area thumb rendered outside a scroll area scrollbar.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static ScrollAreaScrollbarContext Require(ScrollAreaScrollbarContext? context)
    {
        // SCR-EDGE-03: Base UI's wording with the product prefix and code in backticks.
        return context ?? throw new InvalidOperationException(
            "Blazix: `ScrollAreaScrollbarContext` is missing. ScrollAreaScrollbar parts must be placed within `<ScrollAreaScrollbar>`.");
    }

    /// <summary>Sets the thumb's listener, which runs when the orientation changes.</summary>
    /// <param name="value">The listener, or <see langword="null"/> to remove it.</param>
    public void SetListener(Action? value) => listener = value;

    /// <summary>Takes the scrollbar's orientation and notifies the thumb when it changed.</summary>
    /// <param name="orientation">The orientation.</param>
    public void SetOrientation(Orientation orientation)
    {
        if (orientation == Orientation)
        {
            return;
        }

        Orientation = orientation;
        listener?.Invoke();
    }

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<ScrollAreaScrollbarContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ScrollAreaScrollbarContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<ScrollAreaScrollbarContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ScrollAreaScrollbarContext>.ChildContent), content);
        builder.CloseComponent();
    };
}
