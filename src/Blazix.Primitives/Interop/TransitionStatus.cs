namespace Blazix.Primitives;

// TRN §3.3 and TRN-STA-01 (the unset status is a null value); ADR 08 §3 (Enums row, its own example), placed beside
// its first consumer, TransitionsTwin, with the shared namespace like Events/.
/// <summary>
/// Specifies the phase of an animated part's enter or exit transition.
/// </summary>
/// <remarks>
/// A part's state exposes the phase as a nullable value: <see langword="null"/> is the unset status, meaning no
/// transition is in progress (the part is settled open, or closed). <see cref="Idle"/> is reported instead of
/// <see langword="null"/> for a settled open part only by components that ask for it, such as a collapsible panel.
/// </remarks>
public enum TransitionStatus
{
    /// <summary>The part has just been shown and carries <c>data-starting-style</c> until the next frame (<c>starting</c>).</summary>
    Starting,

    /// <summary>The part is closing and carries <c>data-ending-style</c> until its exit animations finish (<c>ending</c>).</summary>
    Ending,

    /// <summary>The part is open and its enter transition has settled (<c>idle</c>).</summary>
    Idle,
}
