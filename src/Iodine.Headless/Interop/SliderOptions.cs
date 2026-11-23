namespace Iodine.Headless.Interop;

// ADR 04 §1 (the `slider` component module), §2 (the Slider.Thumb and Slider.Indicator row) and Amendments 2026-10-18 (the
// handover) and 2026-11-18 (the bands, the revision rule). Design point 1 (a') of track 4: one handle on the Root element,
// which is the key surface (SLD-STR-05: a thumb anywhere under the Root takes keys); the Control and the Indicators cross
// as references (ADR 04 Amendments 2026-10-27), the thumbs as their inputs' ids by index, from which the module finds each
// thumb element as the input's parent. C# owns the values; the module computes every candidate (SLD §4.1) and moves the
// thumbs at once, and an `update` older than the module's last act is ignored unless it carries a rollback. Handlers read
// the options of the latest update.
/// <summary>
/// The options of the <c>slider</c> module, sent on <c>attach</c> and on every <c>update</c>: the values, the bounds, the
/// thumbs and the flags of one slider.
/// </summary>
internal sealed record SliderOptions : TwinOptions
{
    /// <summary>Gets the normalized values, one per thumb.</summary>
    /// <value>The values, clamped and sorted.</value>
    public IReadOnlyList<double> Values { get; init; } = [];

    /// <summary>Gets a value indicating whether the slider is in range mode.</summary>
    /// <value><see langword="true"/> when the value is a list, even of one element.</value>
    public bool Range { get; init; }

    /// <summary>Gets the lower bound.</summary>
    /// <value>The minimum.</value>
    public double Min { get; init; }

    /// <summary>Gets the upper bound.</summary>
    /// <value>The maximum.</value>
    public double Max { get; init; } = 100;

    /// <summary>Gets the step.</summary>
    /// <value>The step of the arrow keys and the grid of pointer input.</value>
    public double Step { get; init; } = 1;

    /// <summary>Gets the large step.</summary>
    /// <value>The step of the Page keys and of Shift with an arrow key.</value>
    public double LargeStep { get; init; } = 10;

    /// <summary>Gets the minimum number of steps between adjacent values.</summary>
    /// <value>The minimum distance in steps.</value>
    public double MinStepsBetweenValues { get; init; }

    /// <summary>Gets a value indicating whether the slider is vertical.</summary>
    /// <value><see langword="true"/> for a vertical slider.</value>
    public bool Vertical { get; init; }

    // Design point 2 (a) of track 4: the direction is the provider's, never `dir` (SLD-EDGE-13, UTL-I18N-03).
    /// <summary>Gets a value indicating whether the nearest direction provider is right to left.</summary>
    /// <value><see langword="true"/> for right to left.</value>
    public bool Rtl { get; init; }

    /// <summary>Gets a value indicating whether the slider is disabled.</summary>
    /// <value><see langword="true"/> while disabled, its own flag or its field's.</value>
    public bool Disabled { get; init; }

    /// <summary>Gets a value indicating whether a thumb at a bound stays inside the control.</summary>
    /// <value><see langword="true"/> for the edge alignments.</value>
    public bool Inset { get; init; }

    /// <summary>Gets the collision behavior of a dragged range thumb.</summary>
    /// <value><c>push</c>, the default, <c>swap</c> or <c>none</c>.</value>
    public string Collision { get; init; } = "push";

    /// <summary>Gets the thumbs, in index order.</summary>
    /// <value>One entry per index from 0 to the highest registered index; an index with no thumb has no input id.</value>
    public IReadOnlyList<SliderThumbOptions> Thumbs { get; init; } = [];

    /// <summary>Gets the index in the references of the control element.</summary>
    /// <value>The index, or <see langword="null"/> while no control is mounted.</value>
    public int? ControlRef { get; init; }

    /// <summary>Gets the indicators.</summary>
    /// <value>Each mounted indicator's reference index and the consumer's style.</value>
    public IReadOnlyList<SliderIndicatorOptions> Indicators { get; init; } = [];

    // ADR 04 Amendments 2026-11-18 (the revision rule): the revision of the last report C# has applied or refused.
    /// <summary>Gets the revision of the last report this update answers.</summary>
    /// <value>The report's revision, 0 before any report.</value>
    public int Revision { get; init; }

    /// <summary>Gets a value indicating whether this update undoes the module's acts after <see cref="Revision"/>.</summary>
    /// <value><see langword="true"/> when C# refused a report (a cancel, or a controlled value the consumer did not change).</value>
    public bool Rollback { get; init; }

    // SLD-STA-09, SLD-EVT-10: C# compares a candidate with the raw value, so an unsorted or out-of-range controlled value
    // reports a candidate equal to the normalized one.
    /// <summary>Gets a value indicating whether the raw value differs from the normalized values.</summary>
    /// <value><see langword="true"/> to report a candidate even when it equals <see cref="Values"/>.</value>
    public bool RawDiffers { get; init; }

    /// <summary>Gets a value indicating whether a thumb's consumer has a C# <c>onkeydown</c> handler.</summary>
    /// <value><see langword="true"/> to ask the key decision point, and to report each key where no C# chain ran inside the dispatch.</value>
    public bool KeyHandlers { get; init; }

    /// <summary>Gets a value indicating whether the control's consumer has a C# <c>onpointerdown</c> handler.</summary>
    /// <value><see langword="true"/> to ask the press decision point before a gesture starts.</value>
    public bool PressHandler { get; init; }

    // ADR 16 rule 6: an unchanged render sends no `update`, so the lists compare by element, not by reference (the
    // FormSubmitOptions precedent).
    /// <inheritdoc />
    public bool Equals(SliderOptions? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Style, other.Style, StringComparison.Ordinal)
            && Range == other.Range
            && Min.Equals(other.Min)
            && Max.Equals(other.Max)
            && Step.Equals(other.Step)
            && LargeStep.Equals(other.LargeStep)
            && MinStepsBetweenValues.Equals(other.MinStepsBetweenValues)
            && Vertical == other.Vertical
            && Rtl == other.Rtl
            && Disabled == other.Disabled
            && Inset == other.Inset
            && string.Equals(Collision, other.Collision, StringComparison.Ordinal)
            && ControlRef == other.ControlRef
            && Revision == other.Revision
            && Rollback == other.Rollback
            && RawDiffers == other.RawDiffers
            && KeyHandlers == other.KeyHandlers
            && PressHandler == other.PressHandler
            && SameItems(Values, other.Values)
            && SameItems(Thumbs, other.Thumbs)
            && SameItems(Indicators, other.Indicators);
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Values.Count, Thumbs.Count, Indicators.Count, Revision, Min, Max);

    private static bool SameItems<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < left.Count; i++)
        {
            if (!comparer.Equals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>The options of one thumb of the <c>slider</c> module.</summary>
internal sealed record SliderThumbOptions
{
    /// <summary>Gets the id of the thumb's input.</summary>
    /// <value>The id, or <see langword="null"/> for an index no thumb takes.</value>
    public string? InputId { get; init; }

    /// <summary>Gets a value indicating whether the thumb is disabled, its own flag or the slider's.</summary>
    /// <value><see langword="true"/> while disabled.</value>
    public bool Disabled { get; init; }

    /// <summary>Gets the consumer's merged style of the thumb element, under which the module writes its own.</summary>
    /// <value>The style string, or <see langword="null"/>.</value>
    public string? Style { get; init; }
}

/// <summary>The options of one indicator of the <c>slider</c> module.</summary>
internal sealed record SliderIndicatorOptions
{
    /// <summary>Gets the index in the references of the indicator element.</summary>
    /// <value>The index.</value>
    public int Ref { get; init; }

    /// <summary>Gets the consumer's merged style of the indicator, under which the module writes its own.</summary>
    /// <value>The style string, or <see langword="null"/>.</value>
    public string? Style { get; init; }
}
