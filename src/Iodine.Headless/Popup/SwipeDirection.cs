namespace Blazix.Primitives;

// ADR 08 Amendments 2026-11-18, the Swipe line (chosen by the user 2026-11-18, the M6 plan's decision 24): the PascalCase of
// Base UI's `'up' | 'down' | 'left' | 'right'`, shared by Drawer and Toast. In Popup/ beside the swipe tracker's contract
// (PSH §4.7.5), the InteractionType precedent (M6 track 1's design point 1 (a), settled by the orchestrator 2026-11-18). Physical, never
// mirrored (PSH-I18N-02).
/// <summary>Specifies a physical swipe direction.</summary>
/// <remarks>The directions are physical: they are not mirrored in a right-to-left layout.</remarks>
public enum SwipeDirection
{
    /// <summary>The pointer moves up.</summary>
    Up,

    /// <summary>The pointer moves down.</summary>
    Down,

    /// <summary>The pointer moves left.</summary>
    Left,

    /// <summary>The pointer moves right.</summary>
    Right,
}
