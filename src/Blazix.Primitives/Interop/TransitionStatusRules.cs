namespace Blazix.Primitives.Interop;

// TRN-ALG-01 (the evaluation step and init), TRN-STA-02/03/06/08/12/13. The same step runs in the transitions module
// for an element that is already attached (ADR 01 Amendments 2026-10-21, decision 2 note); one case table tests both:
// src/Blazix.Primitives/js/test-support/transition-evaluation.cases.json, read at L1 and L3.
/// <summary>
/// The rules of the transition status machine that a part applies in C#: the initial values of a part's first render
/// and the evaluation step that a render applying a new open state carries out. The frame-timed steps run in
/// JavaScript.
/// </summary>
internal static class TransitionStatusRules
{
    /// <summary>Returns the mounted flag and the status of a part's first render.</summary>
    /// <param name="open">Whether the part is open on its first render.</param>
    /// <param name="enableIdleState">Whether a settled open part reports <see cref="TransitionStatus.Idle"/>.</param>
    /// <param name="animateInitialOpen">Whether a part that is open on its first render still plays its enter transition.</param>
    /// <returns>
    /// Not mounted and unset when closed; mounted and <see cref="TransitionStatus.Idle"/> or unset when open; mounted and
    /// <see cref="TransitionStatus.Starting"/> when open with <paramref name="animateInitialOpen"/>.
    /// </returns>
    public static (bool Mounted, TransitionStatus? Status) Initial(bool open, bool enableIdleState, bool animateInitialOpen)
    {
        TransitionStatus? status = open && enableIdleState ? TransitionStatus.Idle : null;
        return Evaluate(open, open && !animateInitialOpen, status, deferEndingState: false);
    }

    /// <summary>Returns the mounted flag and the status that the render applying a new open state carries.</summary>
    /// <param name="open">Whether the part is open.</param>
    /// <param name="mounted">The mounted flag before the render.</param>
    /// <param name="status">The status before the render, or <see langword="null"/> for the unset status.</param>
    /// <param name="deferEndingState">Whether <see cref="TransitionStatus.Ending"/> is applied one frame after the closing render.</param>
    /// <returns>
    /// Mounted and <see cref="TransitionStatus.Starting"/> for an open part that was not mounted;
    /// <see cref="TransitionStatus.Ending"/> for a closed, mounted part unless the ending is deferred; the unset status
    /// for a closed part that is no longer mounted while ending; otherwise the values unchanged.
    /// </returns>
    public static (bool Mounted, TransitionStatus? Status) Evaluate(bool open, bool mounted, TransitionStatus? status, bool deferEndingState)
    {
        if (open && !mounted)
        {
            return (true, TransitionStatus.Starting);
        }

        if (!open && mounted && status != TransitionStatus.Ending && !deferEndingState)
        {
            return (true, TransitionStatus.Ending);
        }

        if (!open && !mounted && status == TransitionStatus.Ending)
        {
            return (false, null);
        }

        return (mounted, status);
    }
}
