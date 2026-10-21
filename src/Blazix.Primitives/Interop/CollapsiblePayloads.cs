namespace Blazix.Primitives.Interop;

// COL-STA-06 (the panel's status is forced to idle during a motion-skipping open)
/// <summary>The payload of the forced-idle report: whether the panel shows itself settled while the shared status is still starting.</summary>
/// <param name="ForcedIdle"><see langword="true"/> from a motion-skipping open until the shared status leaves its starting phase.</param>
internal sealed record PanelForcedIdleChange(bool ForcedIdle);

// COL-ANI-01/02, COL-EDGE-03 (the detected animation type and the mixed-motion warning)
/// <summary>The payload of the animation type report: how the panel animates, as last detected.</summary>
/// <param name="Type">The literal <c>css-transition</c>, <c>css-animation</c> or <c>none</c>.</param>
/// <param name="Mixed"><see langword="true"/> when both a transition and a keyframe animation were found.</param>
internal sealed record PanelAnimationTypeChange(string Type, bool Mixed);
