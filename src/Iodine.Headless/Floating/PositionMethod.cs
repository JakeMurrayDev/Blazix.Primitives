namespace Blazix.Primitives;

// ADR 08 §3 (Enums row) and Amendments 2026-10-27; Base UI's `positionMethod` (internals/useAnchorPositioning.ts:681).
/// <summary>Specifies the CSS <c>position</c> a positioned popup uses.</summary>
public enum PositionMethod
{
    /// <summary><c>position: absolute</c>.</summary>
    Absolute,

    /// <summary><c>position: fixed</c>.</summary>
    Fixed,
}
