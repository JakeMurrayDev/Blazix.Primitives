namespace Blazix.Primitives.Interop;

// ADR 04 §1 and §3; UBT-API-01, UBT-ALG-01..07, UBT-EDGE-01..03 (button-behavior.md §6.1 and §6.3)
/// <summary>
/// The options of the <c>button</c> module: the mechanism's configuration, sent on <c>attach</c> and on every
/// <c>update</c>. The part renders the button attributes itself; the module owns keyboard activation, press
/// suppression and the press-and-hold repeater.
/// </summary>
/// <remarks>The module owns no style property, so a part leaves <see cref="TwinOptions.Style"/> unset.</remarks>
internal sealed record ButtonOptions : TwinOptions
{
    /// <summary>Gets a value indicating whether the element ignores presses and keyboard activation.</summary>
    /// <value><see langword="true"/> while the part is disabled; the default is <see langword="false"/>.</value>
    public bool Disabled { get; init; }

    /// <summary>Gets a value indicating whether a disabled element stays focusable and in the tab order.</summary>
    /// <value>
    /// <see langword="true"/> or <see langword="false"/> when the part specifies it; <see langword="null"/>, the default,
    /// when it does not, in which case a composite item counts as focusable.
    /// </value>
    public bool? FocusableWhenDisabled { get; init; }

    /// <summary>Gets a value indicating whether the element is declared a native <c>&lt;button&gt;</c>.</summary>
    /// <value><see langword="true"/>, the default, for native mode; <see langword="false"/> for an element that is not a button.</value>
    public bool Native { get; init; } = true;

    /// <summary>Gets a value indicating whether the element is an item of a composite widget.</summary>
    /// <value>
    /// <see langword="true"/> when Space activates on keydown, as in a toolbar or a menu: the part's explicit value, or
    /// whether it renders inside a composite root. The default is <see langword="false"/>.
    /// </value>
    public bool Composite { get; init; }

    /// <summary>Gets the press-and-hold repeater's timing, which turns the repeater on.</summary>
    /// <value>The timing, or <see langword="null"/>, the default, for no repeater.</value>
    public PressAndHoldOptions? PressAndHold { get; init; }

    /// <summary>Gets a value indicating whether the module reports how each press happened.</summary>
    /// <value><see langword="true"/> to receive <see cref="ButtonTwin.InteractionReported"/>; the default is <see langword="false"/>.</value>
    public bool ReportInteractionType { get; init; }

    /// <summary>Gets a value indicating whether the module checks that the element matches <see cref="Native"/>.</summary>
    /// <value>The state of the library's development warnings switch unless set.</value>
    public bool DevelopmentChecks { get; init; } = DevelopmentWarnings.IsEnabled;
}

// UBT-ALG-07; button-behavior.md §2 (timing constants)
/// <summary>The timing of the press-and-hold repeater.</summary>
internal sealed record PressAndHoldOptions
{
    /// <summary>Gets the interval between repeated ticks.</summary>
    /// <value>The interval in milliseconds; the default is 60.</value>
    public int TickDelay { get; init; } = 60;

    /// <summary>Gets the delay between the first tick and the first repeated tick.</summary>
    /// <value>The delay in milliseconds; the default is 400.</value>
    public int StartDelay { get; init; } = 400;

    /// <summary>Gets the touch movement that cancels the hold as a scroll.</summary>
    /// <value>The distance in CSS pixels; the default is 8.</value>
    public int ScrollDistance { get; init; } = 8;
}
