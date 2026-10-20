namespace Blazix.Primitives.Rendering;

// RND-STA-02
/// <summary>The state of a part or <see cref="RenderElement{TState}"/> that has none: projects nothing.</summary>
[PartState]
public sealed partial record EmptyState
{
    /// <summary>The shared instance.</summary>
    public static EmptyState Instance { get; } = new();
}
