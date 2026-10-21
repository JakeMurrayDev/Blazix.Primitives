using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (transitions row), §2 (every animated part: data-starting-style, data-ending-style; the convergent write)
// and Amendments 2026-10-18 (handover at attach); TRN-ALG-01, TRN §6.1 (machine options per consumer), TRN-API-01..03,
// TRN-DOM-06..09, TRN-ALG-04 (completion watchers), TRN-ALG-07 (reveal). AnimateInitialOpen has no field: C# renders
// the initial status (TRN-STA-02/12/13) and sends it as Status, so the module starts from what was rendered.
// OpenSelector: design point 1, option B (track 5 note), the open input read from the applied batch (ADR 01 decision 4,
// "post-commit, pre-paint, DOM-only work").
/// <summary>
/// The options of the <c>transitions</c> module, sent on <c>attach</c> and on every <c>update</c>. From
/// <c>attach</c> on, the module writes the element's <c>data-starting-style</c> and <c>data-ending-style</c> and
/// reports the status back; the part keeps rendering every other attribute.
/// </summary>
/// <remarks>
/// <see cref="TwinOptions.Style"/> is applied only when it is set: a part whose style the module does not own leaves
/// it <see langword="null"/> and keeps rendering its style.
/// </remarks>
internal sealed record TransitionsOptions : TwinOptions
{
    /// <summary>Gets a value indicating whether the part is open, which is the input of the transition machine.</summary>
    /// <value><see langword="true"/> while open; the default is <see langword="false"/>.</value>
    public bool Open { get; init; }

    /// <summary>Gets a value indicating whether the part was mounted when its element was rendered.</summary>
    /// <value>The mounted flag of the render the element was attached in; read on <c>attach</c> only.</value>
    public bool Mounted { get; init; }

    /// <summary>Gets the transition status the element was rendered with.</summary>
    /// <value>The status of the render the element was attached in, or <see langword="null"/> for the unset status; read on <c>attach</c> only.</value>
    [JsonConverter(typeof(TransitionStatusJsonConverter))]
    public TransitionStatus? Status { get; init; }

    /// <summary>Gets a value indicating whether a settled open part reports <see cref="TransitionStatus.Idle"/> instead of the unset status.</summary>
    /// <value>
    /// <see langword="true"/> also makes a reopen during the exit pass through <see cref="TransitionStatus.Starting"/>;
    /// the default is <see langword="false"/>.
    /// </value>
    public bool EnableIdleState { get; init; }

    /// <summary>Gets a value indicating whether <see cref="TransitionStatus.Ending"/> is applied one frame after the closing render instead of in it.</summary>
    /// <value><see langword="true"/> to keep the open styles for one frame, during which the part can be measured; the default is <see langword="false"/>.</value>
    public bool DeferEndingState { get; init; }

    /// <summary>Gets a value indicating whether the element stays in the document while the part is not mounted.</summary>
    /// <value><see langword="false"/>, the default, when the part removes the element once its exit has finished.</value>
    public bool KeepMounted { get; init; }

    /// <summary>Gets a value indicating whether the closed, unmounted element is hidden with <c>hidden="until-found"</c> so that find-in-page can reveal it.</summary>
    /// <value><see langword="true"/> to write the literal and keep <c>data-starting-style</c> while hidden; the default is <see langword="false"/>.</value>
    public bool HiddenUntilFound { get; init; }

    /// <summary>Gets a value indicating whether a kept element carries <c>hidden</c> while the part is not mounted.</summary>
    /// <value>
    /// <see langword="true"/> for a container, which the module hides as soon as its exit finishes, ahead of the render
    /// that hides it; <see langword="false"/>, the default, for an indicator-style element that stays visible.
    /// </value>
    public bool HiddenWhenUnmounted { get; init; }

    /// <summary>Gets a value indicating whether the module never writes <c>data-ending-style</c> and reports the ending phase as the unset status.</summary>
    /// <value><see langword="true"/> for a kept element that should not play an exit transition; its exit is still awaited. The default is <see langword="false"/>.</value>
    public bool SuppressEndingStyle { get; init; }

    /// <summary>Gets a value indicating whether the module also reports when the enter animations of an open have finished.</summary>
    /// <value><see langword="true"/> to receive <see cref="TransitionsTwin.TransitionCompleted"/> for the open side; the default is <see langword="false"/>.</value>
    public bool ReportOpenComplete { get; init; }

    /// <summary>Gets a value indicating whether the module listens for the browser's <c>beforematch</c> event on the element.</summary>
    /// <value><see langword="true"/> to receive the reveal decision (<see cref="TransitionsTwin.RevealDecider"/>) and the reveal request (<see cref="TransitionsTwin.RevealRequestHandler"/>); the default is <see langword="false"/>.</value>
    public bool RevealOnBeforeMatch { get; init; }

    /// <summary>Gets the number of times the part has asked the module to unmount the element.</summary>
    /// <value>
    /// A count that the part increases to end the exit itself, for example when another part's exit has finished; an
    /// increase while the part is open is dropped. The default is 0.
    /// </value>
    public int UnmountRequests { get; init; }

    /// <summary>Gets the CSS selector that the element matches while the part renders it open.</summary>
    /// <value>
    /// A selector over attributes the part renders, such as <c>[data-open]</c>. When set, the module reads the open
    /// state from the element after every render, before paint, and no longer reads <see cref="Open"/> after
    /// <c>attach</c>; <see langword="null"/>, the default, takes the open state from <see cref="Open"/>.
    /// </value>
    public string? OpenSelector { get; init; }
}
