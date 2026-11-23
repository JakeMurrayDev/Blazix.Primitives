using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

// COL-STA-06 (the panel's status is forced to idle during a motion-skipping open)
/// <summary>The payload of the forced-idle report: whether the panel shows itself settled while the shared status is still starting.</summary>
/// <param name="ForcedIdle"><see langword="true"/> from a motion-skipping open until the shared status leaves its starting phase.</param>
internal sealed record PanelForcedIdleChange(bool ForcedIdle);

// ACC-STA-03/09 (the status of a panel whose shared state has no root twin; D1 option (a), settled by the orchestrator 2026-10-26)
/// <summary>The payload of the status report: the transition status the panel's machine has, before the forced idle.</summary>
/// <param name="Status">The status; never the unset status, which the end of the exit implies.</param>
internal sealed record PanelStatusChange([property: JsonConverter(typeof(TransitionStatusJsonConverter))] TransitionStatus? Status);

// COL-ANI-01/02, COL-EDGE-03 (the detected animation type and the mixed-motion warning)
/// <summary>The payload of the animation type report: how the panel animates, as last detected.</summary>
/// <param name="Type">The literal <c>css-transition</c>, <c>css-animation</c> or <c>none</c>.</param>
/// <param name="Mixed"><see langword="true"/> when both a transition and a keyframe animation were found.</param>
internal sealed record PanelAnimationTypeChange(string Type, bool Mixed);
