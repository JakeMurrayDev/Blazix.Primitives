using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// TLB §11 (the ToolbarGroupContext the buttons, inputs and toggle groups read, optionally); toolbar/group/
// ToolbarGroupContext.ts:4-12. Design point 14.4 (a), settled by the orchestrator 2026-10-27: the listener list of
// ToolbarRootContext. A part reads the toolbar's disabled state and the group's, so either change reaches it; the
// comparison keeps it to one render.
/// <summary>The state a toolbar group shares with the parts in its subtree.</summary>
internal sealed class ToolbarGroupContext
{
    private readonly List<Action> listeners = [];

    /// <summary>Initializes a new instance of the <see cref="ToolbarGroupContext"/> class.</summary>
    /// <param name="disabled">The group's effective disabled state at its first render.</param>
    public ToolbarGroupContext(bool disabled)
    {
        Disabled = disabled;
    }

    // TLB-API-15: the toolbar's disabled state or the group's own.
    /// <summary>Gets a value indicating whether the group is disabled.</summary>
    /// <value>The group's effective disabled state.</value>
    public bool Disabled { get; private set; }

    /// <summary>Takes the group's state and notifies the parts when it changed.</summary>
    /// <param name="disabled">The group's effective disabled state.</param>
    public void SetState(bool disabled)
    {
        if (disabled == Disabled)
        {
            return;
        }

        Disabled = disabled;

        // A listener may remove itself or another: iterating backwards with a bounds check never skips one still registered.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }

    /// <summary>Adds a listener that runs after every change of the group's state.</summary>
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
        builder.OpenComponent<CascadingValue<ToolbarGroupContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ToolbarGroupContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<ToolbarGroupContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ToolbarGroupContext>.ChildContent), content);
        builder.CloseComponent();
    };
}
