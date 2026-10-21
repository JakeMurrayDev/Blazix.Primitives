using System.Globalization;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Meter.Root (meter/root/MeterRoot.tsx); the contract is docs/components/meter.md. MTR-STA-01: the state is {}
// (EmptyState.Instance, which projects nothing, MTR-DOM-02). MTR-STR-01 / MTR-DOM-01: the consumer's content, then one
// visually hidden span, both inside the root's element (MeterRootContext.Cascade). MTR-API-01: every output derives from
// this render's parameters. MTR-ARIA-01..06 are written from AddPartAttributes, before the consumer's attributes (MTR-DOM-08).
/// <summary>
/// Represents the container of a meter: Base UI's <c>Meter.Root</c>, rendered as a <c>div</c> with the <c>meter</c> role.
/// It groups the other meter parts and exposes the value and its range to assistive technology.
/// </summary>
/// <remarks>
/// The root owns no value: each render derives the clamped value, the percentage, the formatted text and the ARIA
/// attributes from its parameters. A <c>div</c> with <c>role="meter"</c> carries <c>aria-valuenow</c>,
/// <c>aria-valuemin</c>, <c>aria-valuemax</c>, <c>aria-valuetext</c> and, while a <see cref="MeterLabel"/> is present,
/// <c>aria-labelledby</c>; one visually hidden <c>span</c> follows the child content. An attribute the consumer supplies,
/// such as <c>aria-valuetext</c>, replaces the built-in one.
/// </remarks>
public sealed class MeterRoot : PartBase<EmptyState>
{
    // 1. constants
    private const string DefaultTag = "div";

    // 2. fields
    private readonly MeterRootContext context = new();
    private RenderFragment? cascade;

    // constructors follow the fields
    /// <summary>Initializes a new instance of the <see cref="MeterRoot"/> class.</summary>
    public MeterRoot()
    {
        context.Label.Changed = OnLabelChanged;
    }

    // 4. parameter properties
    /// <summary>Gets or sets the current value.</summary>
    /// <value>The value; the part never stores it. A value outside the range is limited to it, and <see cref="double.NaN"/> reads as <see cref="Min"/>.</value>
    [Parameter]
    [EditorRequired]
    public double Value { get; set; }

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
    /// A function that receives the formatted value and the raw value, as the root received it, and returns the text of
    /// <c>aria-valuetext</c>; or <see langword="null"/> to use the formatted value. It runs on every render of the root.
    /// </value>
    [Parameter]
    public Func<string, double, string>? GetAriaValueText { get; set; }

    // 5. public properties (protected members order as public: the base's hooks the part fills in)
    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    // 7. lifecycle methods
    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // MTR-API-01, MTR-STA-02: one computation per change of the inputs, read by every part.
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
    // MTR-ARIA-01..06. aria-valuetext is the formatted value or the function's result, which runs on every render (MTR-EVT-01,
    // no memoization) with the raw value (MTR-API-06). aria-labelledby is written only while a label is registered.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "meter");
        attributes.Set("aria-valuenow", context.ValueNowText);
        attributes.Set("aria-valuemin", context.MinText);
        attributes.Set("aria-valuemax", context.MaxText);
        attributes.Set("aria-valuetext", GetAriaValueText is null ? context.FormattedValue : GetAriaValueText(context.FormattedValue, Value));
        string? labelId = context.Label.Id;
        if (labelId is not null)
        {
            attributes.Set("aria-labelledby", labelId);
        }
    }

    // 11. private methods
    // MTR-ARIA-06, MTR-STA-03: the root renders again when the tracked label id changes, unless the consumer supplies its own
    // aria-labelledby, which replaces the tracked id (MTR-API-09), so the render could not change anything.
    private void OnLabelChanged()
    {
        if (AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("aria-labelledby"))
        {
            return;
        }

        StateHasChanged();
    }
}
