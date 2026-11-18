namespace Blazix.Primitives;

// SLD-API-12, SLD-STA-19; slider/root/SliderRoot.tsx:77 (`thumbCollisionBehavior: 'push' | 'swap' | 'none'`). ADR 08 §3's
// Enums row: family-prefixed and one family's, the members the PascalCase of the literals.
/// <summary>Specifies what happens when a dragged thumb of a range slider reaches a neighboring thumb.</summary>
public enum SliderThumbCollisionBehavior
{
    /// <summary>The dragged thumb pushes its neighbors along, keeping the minimum distance between them.</summary>
    Push,

    /// <summary>The dragged thumb passes its neighbor, and the drag continues with the thumb it swapped with.</summary>
    Swap,

    /// <summary>The dragged thumb stops at its neighbor.</summary>
    None,
}
