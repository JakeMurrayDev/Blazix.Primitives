using System.Globalization;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// PRG-STA-01..05, PRG-STR-02, PRG-EDGE-01; progress/root/ProgressRootContext.tsx. Cascaded fixed by ProgressRoot (ADR 16
// rule 9): a change reaches the label, the track, the indicator and the value through the listeners below, never through the
// cascade, and all of them read this one computation (PRG-STA-04). The Root recomputes in OnParametersSet only when one of
// its inputs changed (PRG-API-01: nothing is stored beyond the last computation of the current inputs); the helpers are
// Utilities/ (Meter's), so no algorithm is written twice (ADR 01 decision 2).
/// <summary>The derived values and the label id a progress root shares with its label, track, indicator and value parts.</summary>
internal sealed class ProgressRootContext
{
    /// <summary>Represents the default value text of an indeterminate progress bar, in English, whatever the culture.</summary>
    public const string IndeterminateText = "indeterminate progress";

    /// <summary>Represents the first argument a value template receives while the progress bar is indeterminate.</summary>
    public const string IndeterminateArgument = "indeterminate";

    private const string IndicatorStylePrefix = "inset-inline-start: 0; height: inherit; width: ";

    private readonly List<Action> statusListeners = [];
    private readonly List<Action> indicatorListeners = [];
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
    /// <value>The value, possibly <see langword="null"/>, <see cref="double.NaN"/>, an infinity or outside the range.</value>
    public double? Value { get; private set; }

    /// <summary>Gets the status derived from the value.</summary>
    /// <value>The status.</value>
    public ProgressStatus Status { get; private set; } = ProgressStatus.Indeterminate;

    /// <summary>Gets the position of the value within the range.</summary>
    /// <value>The percentage from 0 to 100, meaningful only while the status is not <see cref="ProgressStatus.Indeterminate"/>.</value>
    public double Percentage { get; private set; }

    /// <summary>Gets the formatted value that the value part shows.</summary>
    /// <value>The text, or the empty string while the status is <see cref="ProgressStatus.Indeterminate"/>.</value>
    public string FormattedValue { get; private set; } = string.Empty;

    /// <summary>Gets the value text the root uses when it has no function and the consumer gave none.</summary>
    /// <value>The formatted value, or <see cref="IndeterminateText"/> while the status is <see cref="ProgressStatus.Indeterminate"/>.</value>
    public string DefaultAriaValueText { get; private set; } = IndeterminateText;

    /// <summary>Gets the clamped value as attribute text.</summary>
    /// <value>The text of <c>aria-valuenow</c>, or <see langword="null"/> while the status is <see cref="ProgressStatus.Indeterminate"/>.</value>
    public string? ValueNowText { get; private set; }

    /// <summary>Gets the minimum as attribute text.</summary>
    /// <value>The text of <c>aria-valuemin</c>.</value>
    public string MinText { get; private set; } = string.Empty;

    /// <summary>Gets the maximum as attribute text.</summary>
    /// <value>The text of <c>aria-valuemax</c>.</value>
    public string MaxText { get; private set; } = string.Empty;

    /// <summary>Gets the inline style of the indicator.</summary>
    /// <value>The declarations that position the indicator and size it to <see cref="Percentage"/>, or <see langword="null"/> while the status is <see cref="ProgressStatus.Indeterminate"/>.</value>
    public string? IndicatorStyle { get; private set; }

    /// <summary>Throws the error of a part rendered outside a progress root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static ProgressRootContext Require(ProgressRootContext? context)
    {
        // PRG-EDGE-01, in every build; the port's words.
        return context ?? throw new InvalidOperationException(
            "Blazix: The `ProgressRoot` context is missing. Progress parts must be placed within `<ProgressRoot>`.");
    }

    /// <summary>Recomputes the derived values from the root's parameters, and notifies the parts whose output changed.</summary>
    /// <param name="value">The raw value, or <see langword="null"/> when the amount of work is unknown.</param>
    /// <param name="min">The lower bound.</param>
    /// <param name="max">The upper bound.</param>
    /// <param name="format">The numeric format string, or <see langword="null"/> for the percentage.</param>
    /// <param name="culture">The culture, or <see langword="null"/> for the current culture.</param>
    public void Update(double? value, double min, double max, string? format, CultureInfo? culture)
    {
        // The effective culture is part of the key: with no culture the current one applies on every render (PRG-API-05).
        CultureInfo effective = culture ?? CultureInfo.CurrentCulture;
        if (initialized
            && SameValue(Value, value)
            && SameBits(minInput, min)
            && SameBits(maxInput, max)
            && string.Equals(formatInput, format, StringComparison.Ordinal)
            && ReferenceEquals(cultureInput, effective))
        {
            return;
        }

        ProgressStatus previousStatus = Status;
        string? previousStyle = IndicatorStyle;
        string previousFormatted = FormattedValue;
        double? previousValue = Value;
        bool wasInitialized = initialized;

        if (!initialized || !SameBits(minInput, min))
        {
            MinText = NumberSerializer.Serialize(min);
        }

        if (!initialized || !SameBits(maxInput, max))
        {
            MaxText = NumberSerializer.Serialize(max);
        }

        // PRG-STA-02: determinate exactly when the value is a finite number.
        if (value is { } number && double.IsFinite(number))
        {
            // PRG-ALG-01, PRG-ALG-02.
            double percentage = RangeMath.ClampedPercentage(number, min, max);
            double clamped = RangeMath.Clamp(number, min, max);
            Status = clamped == max ? ProgressStatus.Complete : ProgressStatus.Progressing;
            Percentage = percentage;
            FormattedValue = NumberFormatting.FormatMeasure(clamped, percentage, format, effective);
            DefaultAriaValueText = FormattedValue;
            ValueNowText = NumberSerializer.Serialize(clamped);
            IndicatorStyle = IndicatorStylePrefix + NumberSerializer.Serialize(percentage) + "%";
        }
        else
        {
            Status = ProgressStatus.Indeterminate;
            Percentage = 0;
            FormattedValue = string.Empty;
            DefaultAriaValueText = IndeterminateText;
            ValueNowText = null;
            IndicatorStyle = null;
        }

        Value = value;
        minInput = min;
        maxInput = max;
        formatInput = format;
        cultureInput = effective;
        initialized = true;

        if (!wasInitialized)
        {
            return;
        }

        bool statusChanged = previousStatus != Status;
        if (statusChanged)
        {
            Notify(statusListeners);
        }

        if (statusChanged || !string.Equals(previousStyle, IndicatorStyle, StringComparison.Ordinal))
        {
            Notify(indicatorListeners);
        }

        if (statusChanged || !SameValue(previousValue, Value) || !string.Equals(previousFormatted, FormattedValue, StringComparison.Ordinal))
        {
            Notify(valueListeners);
        }
    }

    /// <summary>Adds a listener that runs after the status changed (the label and the track).</summary>
    /// <param name="listener">The listener.</param>
    public void AddStatusListener(Action listener) => statusListeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddStatusListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveStatusListener(Action listener) => statusListeners.Remove(listener);

    /// <summary>Adds a listener that runs after the status or the indicator's style changed.</summary>
    /// <param name="listener">The listener.</param>
    public void AddIndicatorListener(Action listener) => indicatorListeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddIndicatorListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveIndicatorListener(Action listener) => indicatorListeners.Remove(listener);

    /// <summary>Adds a listener that runs after the status, the raw value or the formatted value changed.</summary>
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
        builder.OpenComponent<CascadingValue<ProgressRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ProgressRootContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<ProgressRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ProgressRootContext>.ChildContent), content);
        builder.CloseComponent();

        // PRG-DOM-01: after the consumer's children, in every status, one visually hidden span; the NVDA screen reader
        // announces the label because of it (Base UI issue 4184). Inline, never a nested component (ADR 16 rule 10).
        builder.OpenElement(4, "span");
        builder.AddAttribute(5, "role", "presentation");
        builder.AddAttribute(6, "style", VisuallyHidden.Style);
        builder.AddContent(7, "x");
        builder.CloseElement();
    };

    // The same bits are the same number; this tells 0 from -0 and treats NaN as equal to NaN, which a render must not repeat.
    private static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

    private static bool SameValue(double? a, double? b) => a.HasValue == b.HasValue && (!a.HasValue || SameBits(a.Value, b!.Value));

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
