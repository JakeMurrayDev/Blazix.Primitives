using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// TLB §11 (the ToolbarRootContext every part reads); toolbar/root/ToolbarRootContext.ts:5-23. Design point 14.4 of M3 track
// 4, option (a), settled by the orchestrator 2026-10-27: a plain listener list cascaded fixed (ADR 16 rule 9;
// ToggleGroupContext, design point 11.1). A change of the disabled state or the orientation notifies every part, which
// compares what it renders and renders only when that changed; the root's diff never reaches them. ToggleGroup and,
// from M4, the popups read it optionally.
/// <summary>The state a toolbar shares with the parts in its subtree.</summary>
internal sealed class ToolbarRootContext
{
    private readonly List<Action> listeners = [];

    /// <summary>Initializes a new instance of the <see cref="ToolbarRootContext"/> class.</summary>
    /// <param name="disabled">The toolbar's disabled state at its first render.</param>
    /// <param name="orientation">The toolbar's orientation at its first render.</param>
    public ToolbarRootContext(bool disabled, Orientation orientation)
    {
        Disabled = disabled;
        Orientation = orientation;
    }

    // TLB-API-01, TLB-STA-05: OR-combined into each button's, input's, group's and toggle group's effective disabled.
    /// <summary>Gets a value indicating whether the toolbar is disabled.</summary>
    /// <value>The toolbar's <c>Disabled</c> parameter.</value>
    public bool Disabled { get; private set; }

    // TLB-API-02: every part's state carries it; the separator's default is perpendicular to it.
    /// <summary>Gets the toolbar's orientation.</summary>
    /// <value>The toolbar's <c>Orientation</c> parameter.</value>
    public Orientation Orientation { get; private set; }

    /// <summary>Throws the error of a part rendered outside a toolbar.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static ToolbarRootContext Require(ToolbarRootContext? context)
    {
        // TLB-STR-02, TLB-EDGE-01, in every build and before the composite item's own error; the port's words (the
        // PRG-EDGE-01 form, `Blazix: ` for `Base UI: `, chosen by the user 2026-10-26).
        return context ?? throw new InvalidOperationException(
            "Blazix: The `ToolbarRoot` context is missing. Toolbar parts must be placed within `<ToolbarRoot>`.");
    }

    /// <summary>Takes the toolbar's state and notifies the parts when it changed.</summary>
    /// <param name="disabled">The toolbar's disabled state.</param>
    /// <param name="orientation">The toolbar's orientation.</param>
    public void SetState(bool disabled, Orientation orientation)
    {
        if (disabled == Disabled && orientation == Orientation)
        {
            return;
        }

        Disabled = disabled;
        Orientation = orientation;
        Notify();
    }

    /// <summary>Adds a listener that runs after every change of the toolbar's state.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<ToolbarRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ToolbarRootContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<ToolbarRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ToolbarRootContext>.ChildContent), content);
        builder.CloseComponent();
    };

    private void Notify()
    {
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
