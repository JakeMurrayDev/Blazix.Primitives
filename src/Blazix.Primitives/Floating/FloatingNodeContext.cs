namespace Blazix.Primitives;

// ADR 04 Amendments 2026-11-03 (5): the node a part with no fixed context of its own cascades (NavigationMenu's Content, the
// test-only roots). See IFloatingNodeContext.
/// <summary>Carries the session token a nested popup's root joins as its parent in the floating tree.</summary>
/// <param name="sessionToken">The session token of the enclosing popup.</param>
internal sealed class FloatingNodeContext(string? sessionToken) : IFloatingNodeContext
{
    /// <inheritdoc />
    public string? SessionToken { get; } = sessionToken;
}
