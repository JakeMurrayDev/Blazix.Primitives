using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

/// <summary>Represents the state of a progress indicator, which the part passes to its class and style functions and projects as a status data attribute.</summary>
/// <param name="Status">The status derived from the value.</param>
[PartState]
public sealed partial record ProgressIndicatorState(ProgressStatus Status);

/// <summary>Provides the names of the data attributes a progress indicator renders.</summary>
public static class ProgressIndicatorDataAttributes
{
    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Complete"/>.</summary>
    public const string Complete = "data-complete";

    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Indeterminate"/>.</summary>
    public const string Indeterminate = "data-indeterminate";

    /// <summary>Represents the attribute present while the status is <see cref="ProgressStatus.Progressing"/>.</summary>
    public const string Progressing = "data-progressing";
}

// Base UI's Progress.Indicator (progress/indicator/ProgressIndicator.tsx); the contract is docs/components/progress.md.
// PRG-DOM-03: while determinate the inline style is present for every value; PRG-DOM-04: while indeterminate the part adds no
// style at all, so the declarations are gone after the transition and the consumer's stay (§14 "Indeterminate style");
// PRG-DOM-05: the consumer's style wins key by key (the pipeline's style merge, with the part's declarations first). The
// width changes only when the root's percentage does, which reaches this part through the context's listener (the cascade is
// fixed).
/// <summary>
/// Represents the bar that shows the value's position within the range: Base UI's <c>Progress.Indicator</c>, rendered as a
/// <c>div</c>. It belongs inside a <see cref="ProgressRoot"/>.
/// </summary>
/// <remarks>
/// While the progress is determinate the indicator carries the inline style <c>inset-inline-start: 0; height: inherit;
/// width: P%</c>, where P is the percentage with full precision. While it is indeterminate the indicator has no built-in
/// style. A <c>style</c> the consumer supplies replaces the built-in declarations key by key.
/// </remarks>
public sealed class ProgressIndicator : PartBase<ProgressIndicatorState>
{
    // 1. constants
    private const string DefaultTag = "div";

    // 2. fields
    private static readonly StateAttributesMapping<ProgressIndicatorState> s_mapping = ProgressStatusMapping.Create<ProgressIndicatorState>();

    private static readonly ProgressIndicatorState[] s_states =
    [
        new(ProgressStatus.Indeterminate),
        new(ProgressStatus.Progressing),
        new(ProgressStatus.Complete),
    ];

    private readonly Action onIndicatorChanged;
    private ProgressRootContext? context;

    // constructors follow the fields
    /// <summary>Initializes a new instance of the <see cref="ProgressIndicator"/> class.</summary>
    public ProgressIndicator()
    {
        onIndicatorChanged = StateHasChanged;
    }

    // 3. private properties
    [CascadingParameter]
    private ProgressRootContext? RootContext { get; set; }

    // 5. public properties (protected members order as public: the base's hooks the part fills in)
    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ProgressIndicatorState State => s_states[(int)context!.Status];

    /// <inheritdoc />
    protected override StateAttributesMapping<ProgressIndicatorState>? StateAttributesMapping => s_mapping;

    // 7. lifecycle methods
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The indicator is not inside a <see cref="ProgressRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = ProgressRootContext.Require(RootContext);
        base.OnInitialized();
        context.AddIndicatorListener(onIndicatorChanged);
    }

    // 8. dispose
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemoveIndicatorListener(onIndicatorChanged);
        }

        base.Dispose(disposing);
    }

    // 9. public methods (protected ones too)
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (context!.IndicatorStyle is { } style)
        {
            attributes.Set("style", style);
        }
    }
}
