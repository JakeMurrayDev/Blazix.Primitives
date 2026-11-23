using System.Globalization;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// MTR-STA-02, MTR-STA-03, MTR-STR-02, MTR-EDGE-01; meter/root/MeterRootContext.ts. Cascaded fixed by MeterRoot (ADR 16 rule
// 9): a change reaches the Indicator and the Value through the listeners below, never through the cascade, and all of them
// read this one computation, so the width, the text, aria-valuenow and aria-valuetext never disagree for a render
// (MTR-STA-02). The Root recomputes in OnParametersSet only when one of its inputs changed (MTR-API-01: nothing is stored
// beyond the last computation of the current inputs), so a re-render with the same inputs allocates nothing here.
/// <summary>The derived values and the label id a meter root shares with its indicator, value and label parts.</summary>
internal sealed class MeterRootContext
{
    private const string IndicatorStylePrefix = "inset-inline-start: 0; height: inherit; width: ";

    private readonly List<Action> percentageListeners = [];
    private readonly List<Action> valueListeners = [];
    private bool initialized;
    private double minInput;
    private double maxInput;
    private string? formatInput;
    private CultureInfo? cultureInput;

    /// <summary>Gets the label id slot, which the label part publishes its id to.</summary>
    /// <value>The slot; the root re-renders when its id changes.</value>
    public LabelIdSlot Label { get; } = new();

    /// <summary>Gets the raw value the root received, not limited to the range.</summary>
    /// <value>The value, possibly <see cref="double.NaN"/> or outside the range.</value>
    public double Value { get; private set; }

    /// <summary>Gets the position of the value within the range.</summary>
    /// <value>The percentage from 0 to 100.</value>
    public double Percentage { get; private set; }

    /// <summary>Gets the formatted value that the value part shows and the root uses as its default value text.</summary>
    /// <value>The text.</value>
    public string FormattedValue { get; private set; } = string.Empty;

    /// <summary>Gets the clamped value as attribute text.</summary>
    /// <value>The text of <c>aria-valuenow</c>.</value>
    public string ValueNowText { get; private set; } = string.Empty;

    /// <summary>Gets the minimum as attribute text.</summary>
    /// <value>The text of <c>aria-valuemin</c>.</value>
    public string MinText { get; private set; } = string.Empty;

    /// <summary>Gets the maximum as attribute text.</summary>
    /// <value>The text of <c>aria-valuemax</c>.</value>
    public string MaxText { get; private set; } = string.Empty;

    /// <summary>Gets the inline style of the indicator.</summary>
    /// <value>The declarations that position the indicator and size it to <see cref="Percentage"/>.</value>
    public string IndicatorStyle { get; private set; } = string.Empty;

    /// <summary>Throws the error of a part rendered outside a meter root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static MeterRootContext Require(MeterRootContext? context)
    {
        // MTR-EDGE-01, in every build; the port's words.
        return context ?? throw new InvalidOperationException(
            "Blazix: The `MeterRoot` context is missing. Meter parts must be placed within `<MeterRoot>`.");
    }

    /// <summary>Recomputes the derived values from the root's parameters, and notifies the parts whose output changed.</summary>
    /// <param name="value">The raw value.</param>
    /// <param name="min">The lower bound.</param>
    /// <param name="max">The upper bound.</param>
    /// <param name="format">The numeric format string, or <see langword="null"/> for the percentage.</param>
    /// <param name="culture">The culture, or <see langword="null"/> for the current culture.</param>
    public void Update(double value, double min, double max, string? format, CultureInfo? culture)
    {
        // The effective culture is part of the key: with no culture the current one applies on every render (MTR-API-05).
        CultureInfo effective = culture ?? CultureInfo.CurrentCulture;
        if (initialized
            && SameBits(Value, value)
            && SameBits(minInput, min)
            && SameBits(maxInput, max)
            && string.Equals(formatInput, format, StringComparison.Ordinal)
            && ReferenceEquals(cultureInput, effective))
        {
            return;
        }

        double previousPercentage = Percentage;
        string previousFormatted = FormattedValue;
        double previousValue = Value;
        bool wasInitialized = initialized;

        // MTR-ALG-01, MTR-ALG-02.
        double percentage = RangeMath.ClampedPercentage(value, min, max);
        double clamped = RangeMath.ClampedValue(value, min, max);

        if (!initialized || !SameBits(minInput, min))
        {
            MinText = NumberSerializer.Serialize(min);
        }

        if (!initialized || !SameBits(maxInput, max))
        {
            MaxText = NumberSerializer.Serialize(max);
        }

        ValueNowText = NumberSerializer.Serialize(clamped);
        if (!initialized || !SameBits(previousPercentage, percentage))
        {
            IndicatorStyle = IndicatorStylePrefix + NumberSerializer.Serialize(percentage) + "%";
        }

        FormattedValue = NumberFormatting.FormatMeasure(clamped, percentage, format, effective);
        Value = value;
        Percentage = percentage;
        minInput = min;
        maxInput = max;
        formatInput = format;
        cultureInput = effective;
        initialized = true;

        if (!wasInitialized)
        {
            return;
        }

        if (!SameBits(previousPercentage, percentage))
        {
            Notify(percentageListeners);
        }

        if (!SameBits(previousValue, value) || !string.Equals(previousFormatted, FormattedValue, StringComparison.Ordinal))
        {
            Notify(valueListeners);
        }
    }

    /// <summary>Adds a listener that runs after the percentage changed.</summary>
    /// <param name="listener">The listener.</param>
    public void AddPercentageListener(Action listener) => percentageListeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddPercentageListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemovePercentageListener(Action listener) => percentageListeners.Remove(listener);

    /// <summary>Adds a listener that runs after the raw value or the formatted value changed.</summary>
    /// <param name="listener">The listener.</param>
    public void AddValueListener(Action listener) => valueListeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddValueListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveValueListener(Action listener) => valueListeners.Remove(listener);

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/> and appends the root's hidden span.</summary>
    /// <param name="content">The consumer's content, which receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<MeterRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<MeterRootContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<MeterRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<MeterRootContext>.ChildContent), content);
        builder.CloseComponent();

        // MTR-DOM-01: after the consumer's children, one visually hidden span; the NVDA screen reader announces the label
        // because of it (Base UI issue 4184). Inline, never a nested component (ADR 16 rule 10).
        builder.OpenElement(4, "span");
        builder.AddAttribute(5, "role", "presentation");
        builder.AddAttribute(6, "style", VisuallyHidden.Style);
        builder.AddContent(7, "x");
        builder.CloseElement();
    };

    // The same bits are the same number; this tells 0 from -0 and treats NaN as equal to NaN, which a render must not repeat.
    private static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

    private static void Notify(List<Action> listeners)
    {
        // A listener may remove itself or another (a part disposed by the render it triggers): iterating backwards with a
        // bounds check never skips a listener that is still registered.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }
}
