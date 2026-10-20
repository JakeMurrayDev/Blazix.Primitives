namespace Blazix.Primitives.Rendering;

// RND-STA-02
/// <summary>
/// Represents the state of a part or <see cref="RenderElement{TState}"/> that has no state; it projects no attributes.
/// </summary>
[PartState]
public sealed partial record EmptyState
{
    /// <summary>Gets the shared instance.</summary>
    /// <value>The single shared <see cref="EmptyState"/> instance.</value>
    public static EmptyState Instance { get; } = new();
}
