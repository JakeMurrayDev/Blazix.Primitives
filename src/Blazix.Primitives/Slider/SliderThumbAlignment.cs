namespace Blazix.Primitives;

// SLD-API-11; slider/root/SliderRoot.tsx:78,311,330 (`thumbAlignment: 'center' | 'edge' | 'edge-client-only'`). ADR 08 §3's
// Enums row: family-prefixed and one family's, the members the PascalCase of the literals. ADR 12 Amendments 2026-11-18
// item 6 (chosen by the user 2026-11-18): the pre-hydration script is withdrawn, so Edge behaves as EdgeClientOnly.
/// <summary>Specifies how a slider thumb at the minimum or the maximum aligns with the edge of the control.</summary>
public enum SliderThumbAlignment
{
    /// <summary>The thumb's center sits on the edge, so the thumb overhangs the control by half its size.</summary>
    Center,

    /// <summary>The thumb stays inside the control: its edge, not its center, meets the control's edge.</summary>
    Edge,

    /// <summary>The thumb stays inside the control, positioned once the browser has measured it.</summary>
    EdgeClientOnly,
}
