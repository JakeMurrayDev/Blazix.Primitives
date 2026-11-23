using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Meter.Indicator (meter/indicator/MeterIndicator.tsx); the contract is docs/components/meter.md. MTR-DOM-03: the
// inline style is present for every value; MTR-DOM-04: the consumer's style wins key by key (the pipeline's style merge, with
// the part's declarations first). MTR-STA-01: the state is {}. The width changes only when the root's percentage does, which
// reaches this part through the context's listener (the cascade is fixed).
/// <summary>
/// Represents the bar that shows the value's position within the range: Base UI's <c>Meter.Indicator</c>, rendered as a
/// <c>div</c>. It belongs inside a <see cref="MeterRoot"/>.
/// </summary>
/// <remarks>
/// The indicator always carries the inline style <c>inset-inline-start: 0; height: inherit; width: P%</c>, where P is the
/// percentage with full precision. A <c>style</c> the consumer supplies replaces those declarations key by key.
/// </remarks>
public sealed class MeterIndicator : PartBase<EmptyState>
{
    // 1. constants
    private const string DefaultTag = "div";

    // 2. fields
    private readonly Action onPercentageChanged;
    private MeterRootContext? context;

    // constructors follow the fields
    /// <summary>Initializes a new instance of the <see cref="MeterIndicator"/> class.</summary>
    public MeterIndicator()
    {
        onPercentageChanged = StateHasChanged;
    }

    // 3. private properties
    [CascadingParameter]
    private MeterRootContext? RootContext { get; set; }

    // 5. public properties (protected members order as public: the base's hooks the part fills in)
    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    // 7. lifecycle methods
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The indicator is not inside a <see cref="MeterRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = MeterRootContext.Require(RootContext);
        base.OnInitialized();
        context.AddPercentageListener(onPercentageChanged);
    }

    // 8. dispose
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemovePercentageListener(onPercentageChanged);
        }

        base.Dispose(disposing);
    }

    // 9. public methods (protected ones too)
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("style", context!.IndicatorStyle);
    }
}
