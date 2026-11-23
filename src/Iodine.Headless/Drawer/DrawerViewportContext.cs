using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// DRW-STR-04 (drawer/viewport/DrawerViewportContext.tsx): what a Drawer viewport provides its popup: that it is inside a
// viewport (DRW-EDGE-01) and the gesture's swiping state, the C# mirror of the `drawer` module's data-swiping (8b; ADR 04
// §2's mirrored field). Cascaded fixed by DrawerViewport (ADR 16 rule 9); a change reaches the popup through the listener.
/// <summary>The state a drawer's viewport gives its popup.</summary>
internal sealed class DrawerViewportContext
{
    private Action? listener;

    /// <summary>Gets a value indicating whether the viewport's swipe gesture is active, as the browser-side behavior last reported.</summary>
    public bool Swiping { get; private set; }

    /// <summary>Records the gesture's swiping state and notifies the popup when it changed.</summary>
    /// <param name="swiping">Whether the gesture is active.</param>
    public void SetSwiping(bool swiping)
    {
        if (Swiping != swiping)
        {
            Swiping = swiping;
            listener?.Invoke();
        }
    }

    /// <summary>Sets the listener the popup renders from, or removes it with <see langword="null"/>.</summary>
    /// <param name="changed">The listener.</param>
    public void SetListener(Action? changed) => listener = changed;

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<DrawerViewportContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<DrawerViewportContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<DrawerViewportContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<DrawerViewportContext>.ChildContent), content);
        builder.CloseComponent();
    };
}
