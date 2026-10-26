namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `tabs` component module), §2 (the Tabs.Indicator row) and Amendments 2026-10-26 (chosen by the user
// 2026-10-26): one handle on the List (the activation and its `select` decision point, the pressing state of
// TAB-PTR-05/06, data-activation-direction on the five parts, the ResizeObserver over the List and its Tabs) and one per
// Indicator (its `hidden` and the six --active-tab-* variables over the consumer's style). Values never cross: the options
// carry flags and literals only, and the module finds its Tabs, Panels and Indicators by ownership (design point 4 of the
// Tabs track, pending the user). Controlled is design point 7's input and ReportClicks/ReportFocus design point 5's, both
// pending the user. Handlers read the options of the latest update.
/// <summary>
/// The options of the <c>tabs</c> module, sent on <c>attach</c> and on every <c>update</c>: a tab list's activation and
/// activation direction, or a tab indicator's measurement.
/// </summary>
internal sealed record TabsOptions : TwinOptions
{
    /// <summary>Gets the part the handle serves.</summary>
    /// <value><see cref="TabsTwin.ListPart"/>, the default, or <see cref="TabsTwin.IndicatorPart"/>.</value>
    public string Part { get; init; } = TabsTwin.ListPart;

    /// <summary>Gets the axis along which the list's tabs are laid out, which decides the axis of the activation direction.</summary>
    /// <value><c>horizontal</c>, the default, or <c>vertical</c>.</value>
    public string Orientation { get; init; } = "horizontal";

    /// <summary>Gets a value indicating whether a tab that receives focus is activated.</summary>
    /// <value><see langword="true"/> to activate on focus, unless a non-primary press is held; the default is <see langword="false"/>.</value>
    public bool ActivateOnFocus { get; init; }

    /// <summary>Gets a value indicating whether the consumer owns the root's value.</summary>
    /// <value>
    /// <see langword="true"/> when every change of the active tab the module did not start is programmatic, so its direction
    /// is computed from the two tabs' positions; <see langword="false"/>, the default, when such a change is an automatic
    /// fallback, whose direction is <c>none</c>.
    /// </value>
    public bool Controlled { get; init; }

    /// <summary>Gets a value indicating whether the module reports a click on an enabled tab that it does not activate.</summary>
    /// <value><see langword="true"/> while a tab of the list has a consumer <c>onclick</c>; the default is <see langword="false"/>.</value>
    public bool ReportClicks { get; init; }

    /// <summary>Gets a value indicating whether the module reports a focus on a tab that it does not activate.</summary>
    /// <value><see langword="true"/> while a tab of the list has a consumer <c>onfocus</c>; the default is <see langword="false"/>.</value>
    public bool ReportFocus { get; init; }

    /// <summary>Gets the consumer's <c>hidden</c> on an indicator, which the module never writes over.</summary>
    /// <value>The consumer's value, or <see langword="null"/>, the default, when the module writes <c>hidden</c> itself.</value>
    public bool? Hidden { get; init; }
}
