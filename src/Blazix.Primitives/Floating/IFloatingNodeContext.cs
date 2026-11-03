namespace Blazix.Primitives;

// ADR 04 Amendments 2026-11-03 (5) (chosen by the user 2026-11-03): the floating tree across families. Base UI's node
// context (FloatingTree.tsx: a node's parent is the nearest node context; FloatingTree never resets it) as one internal,
// family-agnostic cascade of the enclosing popup's session token. Every popup Root reads it as a [CascadingParameter] for
// InteractionsSessionBehavior.ParentToken, whichever family encloses it; a Root, or a part that owns a nested Root's parent
// such as NavigationMenu's Content, cascades it, fixed (BLZX2001). A reader asks for the interface, so a family's own fixed
// context may supply it; a part with none cascades a FloatingNodeContext. Tooltip neither reads nor cascades it (TIP-DIS-08;
// M5 track 2 design point 2, pending the orchestrator).
/// <summary>Defines the popup a nested popup's root joins as its parent in the floating tree.</summary>
internal interface IFloatingNodeContext
{
    /// <summary>Gets the session token of the enclosing popup.</summary>
    /// <value>The token the enclosing popup's root drew, or <see langword="null"/> for a fallback store.</value>
    string? SessionToken { get; }
}
