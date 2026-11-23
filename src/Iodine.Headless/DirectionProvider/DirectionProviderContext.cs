using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// UTL-I18N-01..04 (shared-utilities.md §4.15.1), internals/direction-context/DirectionContext.tsx. Cascaded fixed by
// DirectionProvider (ADR 16 rule 9): a change of direction reaches the readers through the listeners below, never through
// the cascade. Readers: the composite root (CompositeRootBehavior) in M2; positioning, Slider and ScrollArea later. No
// public reader in M2 (Base UI's useDirection; chosen by the user 2026-10-22, UTL §4.15.1 port note). Nothing reads `dir` or a computed
// style (UTL-I18N-03); the logical tree carries the value into portaled subtrees (UTL-I18N-04, ADR 04 §5).
/// <summary>The reading direction a <see cref="DirectionProvider"/> gives its descendants.</summary>
internal sealed class DirectionProviderContext
{
    private readonly List<Action> listeners = [];

    /// <summary>Gets the reading direction.</summary>
    /// <value>The provider's direction; <see cref="TextDirection.Ltr"/> until the provider sets it.</value>
    public TextDirection Direction { get; private set; }

    /// <summary>Reads the direction a reader receives from the nearest provider.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/> without a provider.</param>
    /// <returns>The provider's direction, or <see cref="TextDirection.Ltr"/> without a provider.</returns>
    public static TextDirection Read(DirectionProviderContext? context) => context?.Direction ?? TextDirection.Ltr;

    /// <summary>Sets the direction and notifies the listeners when it changed.</summary>
    /// <param name="direction">The new direction.</param>
    public void SetDirection(TextDirection direction)
    {
        if (direction == Direction)
        {
            return;
        }

        Direction = direction;

        // A listener may remove itself or another: iterating backwards with a bounds check never skips one still there.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }

    /// <summary>Adds a listener that runs after every change of <see cref="Direction"/>.</summary>
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
        builder.OpenComponent<CascadingValue<DirectionProviderContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<DirectionProviderContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<DirectionProviderContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<DirectionProviderContext>.ChildContent), content);
        builder.CloseComponent();
    };
}
