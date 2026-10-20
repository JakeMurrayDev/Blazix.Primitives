namespace Blazix.Primitives.Rendering;

/// <summary>The state of a part or <see cref="RenderElement{TState}"/> that has none (RND-STA-02): projects nothing.</summary>
[PartState]
public sealed partial record EmptyState
{
    /// <summary>The shared instance.</summary>
    public static EmptyState Instance { get; } = new();
}
