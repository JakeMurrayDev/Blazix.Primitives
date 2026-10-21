using System.Globalization;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// PRG-STA-01: the state of every part is `{ status }`; five records (ADR 08 §3 "State record: <Part>State"), one projection
// (ProgressStatusMapping). The default projection would write data-status; PRG-DOM-02 maps the field away.
/// <summary>Represents the state of a progress root, which the part passes to its class and style functions and projects as a status data attribute.</summary>
/// <param name="Status">The status derived from the value.</param>
[PartState]
public sealed partial record ProgressRootState(ProgressStatus Status);

// PRG §3.6: Base UI's exported ProgressRootDataAttributes (progress/root/ProgressRootDataAttributes.ts); the five parts'
// classes are identical.
/// <summary>Provides the names of the data attributes a progress root renders.</summary>
public static class ProgressRootDataAttributes
{
    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Complete"/>.</summary>
    public const string Complete = "data-complete";

    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Indeterminate"/>.</summary>
    public const string Indeterminate = "data-indeterminate";

    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Progressing"/>.</summary>
    public const string Progressing = "data-progressing";
}

// Base UI's Progress.Root (progress/root/ProgressRoot.tsx); the contract is docs/components/progress.md. PRG-STR-01 /
// PRG-DOM-01: the consumer's content, then one visually hidden span, inside the root's element (ProgressRootContext.Cascade).
// PRG-API-01: every output derives from this render's parameters. PRG-ARIA-01..06 are written from AddPartAttributes, before
// the consumer's attributes (PRG-DOM-09). PRG-DOM-02: the status projects as one presence attribute.
/// <summary>
/// Represents the container of a progress bar: Base UI's <c>Progress.Root</c>, rendered as a <c>div</c> with the
/// <c>progressbar</c> role. It groups the other progress parts, derives a <see cref="ProgressStatus"/> from the value and
/// exposes the value and its range to assistive technology.
/// </summary>
/// <remarks>
/// The root owns no value: each render derives the status, the clamped value, the percentage, the formatted text and the
/// ARIA attributes from its parameters. A <see langword="null"/> or non-finite <see cref="Value"/> is indeterminate:
/// <c>aria-valuenow</c> is omitted and <c>aria-valuetext</c> reads <c>indeterminate progress</c>, in English whatever the
/// culture. Every part carries exactly one of <c>data-indeterminate</c>, <c>data-progressing</c> and <c>data-complete</c>.
/// One visually hidden <c>span</c> follows the child content. An attribute the consumer supplies, such as
/// <c>aria-valuetext</c>, replaces the built-in one.
/// </remarks>
public sealed class ProgressRoot : PartBase<ProgressRootState>
{
    // 1. constants
    private const string DefaultTag = "div";

    // 2. fields
    private static readonly StateAttributesMapping<ProgressRootState> s_mapping = ProgressStatusMapping.Create<ProgressRootState>();

    private static readonly ProgressRootState[] s_states =
    [
        new(ProgressStatus.Indeterminate),
        new(ProgressStatus.Progressing),
        new(ProgressStatus.Complete),
    ];

    private readonly ProgressRootContext context = new();
    private RenderFragment? cascade;

    // constructors follow the fields
    /// <summary>Initializes a new instance of the <see cref="ProgressRoot"/> class.</summary>
    public ProgressRoot()
    {
        context.Label.Changed = OnLabelChanged;
    }

    // 4. parameter properties
    /// <summary>Gets or sets the current value.</summary>
    /// <value>
    /// The value; the part never stores it. <see langword="null"/>, <see cref="double.NaN"/> and the infinities mean the
    /// amount of work is unknown (indeterminate). A finite value outside the range is limited to it.
    /// </value>
    [Parameter]
    [EditorRequired]
    public double? Value { get; set; }

    /// <summary>Gets or sets the lower bound of the range.</summary>
    /// <value>The minimum. The default is 0.</value>
    [Parameter]
    public double Min { get; set; }

    /// <summary>Gets or sets the upper bound of the range.</summary>
    /// <value>The maximum. The default is 100. A range with <see cref="Min"/> above it is not validated.</value>
    [Parameter]
    public double Max { get; set; } = 100;

    /// <summary>Gets or sets the numeric format string of the value.</summary>
    /// <value>
    /// A .NET standard or custom numeric format string, which formats the value limited to the range, such as <c>C</c> or
    /// <c>N1</c>; or <see langword="null"/> to show the value's position within the range as a whole percent. The default
    /// is <see langword="null"/>.
    /// </value>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>Gets or sets the culture that formats the value.</summary>
    /// <value>The culture, or <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>. The default is <see langword="null"/>.</value>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    /// <summary>Gets or sets the function that produces the value text announced by assistive technology.</summary>
    /// <value>
    /// A function that receives the formatted value (the empty string while indeterminate) and the raw value, as the root
    /// received it, and returns the text of <c>aria-valuetext</c>; or <see langword="null"/> to use the default text. It
    /// runs on every render of the root, in every status.
    /// </value>
    [Parameter]
    public Func<string, double?, string>? GetAriaValueText { get; set; }

    // 5. public properties (protected members order as public: the base's hooks the part fills in)
    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ProgressRootState State => s_states[(int)context.Status];

    /// <inheritdoc />
    protected override StateAttributesMapping<ProgressRootState>? StateAttributesMapping => s_mapping;

    // 7. lifecycle methods
    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // PRG-API-01, PRG-STA-04: one computation per change of the inputs, read by every part.
        context.Update(Value, Min, Max, Format, Culture);

        // The cascade wraps the consumer's content inside the root's element. ChildContent holds the root's own cascade after a
        // render, so a different value is the consumer's new content; the cascade is rebuilt only then.
        if (cascade is null || !ReferenceEquals(ChildContent, cascade))
        {
            cascade = context.Cascade(ChildContent);
        }

        ChildContent = cascade;
    }

    // 8. dispose
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context.Label.Changed = null;
        }

        base.Dispose(disposing);
    }

    // 9. public methods (protected ones too)
    // PRG-ARIA-01..06. aria-valuenow is absent while indeterminate; aria-valuemin and aria-valuemax stay in every status.
    // aria-valuetext is the default text or the function's result, which runs on every render in every status (PRG-EVT-01,
    // no memoization) with the formatted value ('' while indeterminate) and the raw value (PRG-API-06).
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "progressbar");
        if (context.ValueNowText is { } valueNow)
        {
            attributes.Set("aria-valuenow", valueNow);
        }

        attributes.Set("aria-valuemin", context.MinText);
        attributes.Set("aria-valuemax", context.MaxText);
        attributes.Set("aria-valuetext", GetAriaValueText is null ? context.DefaultAriaValueText : GetAriaValueText(context.FormattedValue, Value));
        string? labelId = context.Label.Id;
        if (labelId is not null)
        {
            attributes.Set("aria-labelledby", labelId);
        }
    }

    // 11. private methods
    // PRG-ARIA-06, PRG-STA-05: the root renders again when the tracked label id changes, unless the consumer supplies its own
    // aria-labelledby, which replaces the tracked id (PRG-API-09), so the render could not change anything.
    private void OnLabelChanged()
    {
        if (AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("aria-labelledby"))
        {
            return;
        }

        StateHasChanged();
    }
}
