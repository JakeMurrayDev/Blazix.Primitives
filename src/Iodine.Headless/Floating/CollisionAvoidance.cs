namespace Blazix.Primitives;

// ADR 08 Amendments 2026-10-27 (a record of three nullable enums); Base UI's `CollisionAvoidance = SideFlipMode |
// SideShiftMode` (internals/useAnchorPositioning.ts:76-127), read as `side || 'flip'`, `align || 'flip'` and
// `fallbackAxisSide || 'end'` (:162-164). FLT-POS-03..05, FLT §6.1 (`POPUP_COLLISION_AVOIDANCE`).
/// <summary>Describes how a positioned popup avoids collisions with its boundary.</summary>
/// <param name="Side">
/// How the popup avoids a collision on the side axis: <see cref="CollisionAvoidanceMode.Flip"/> to the opposite side,
/// <see cref="CollisionAvoidanceMode.Shift"/> along the boundary, or <see cref="CollisionAvoidanceMode.None"/>; when
/// <see langword="null"/>, <see cref="CollisionAvoidanceMode.Flip"/>.
/// </param>
/// <param name="Align">
/// How the popup avoids a collision on the alignment axis: <see cref="CollisionAvoidanceMode.Flip"/> swaps start and end
/// alignment, <see cref="CollisionAvoidanceMode.Shift"/> keeps the alignment and shifts the popup, or
/// <see cref="CollisionAvoidanceMode.None"/>; when <see langword="null"/>, <see cref="CollisionAvoidanceMode.Flip"/>. When
/// <paramref name="Side"/> is <see cref="CollisionAvoidanceMode.Shift"/>, only <see cref="CollisionAvoidanceMode.Shift"/>
/// and <see cref="CollisionAvoidanceMode.None"/> apply.
/// </param>
/// <param name="FallbackAxisSide">
/// Whether the popup falls back to the perpendicular axis when neither side on its preferred axis fits, and which logical
/// side it prefers there; when <see langword="null"/>, <see cref="global::Blazix.Primitives.FallbackAxisSide.End"/>.
/// </param>
/// <remarks>A positioner part whose own <c>CollisionAvoidance</c> parameter is <see langword="null"/> applies its component's default.</remarks>
public sealed record CollisionAvoidance(CollisionAvoidanceMode? Side = null, CollisionAvoidanceMode? Align = null, FallbackAxisSide? FallbackAxisSide = null);
