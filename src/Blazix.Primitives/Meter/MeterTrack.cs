using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// Base UI's Meter.Track (meter/track/MeterTrack.tsx); the contract is docs/components/meter.md. MTR-STR-03: reads no context,
// so it renders anywhere (docs §16 item 3 leaves requiring a root to a decision). MTR-STR-04: structural only, no built-in
// attribute, style or handler; MTR-STA-01: the state is {}.
/// <summary>
/// Represents the track of a meter, which holds the indicator and stands for the whole range: Base UI's
/// <c>Meter.Track</c>, rendered as a <c>div</c>.
/// </summary>
/// <remarks>The track adds nothing to its element; it carries only the attributes the consumer supplies.</remarks>
public sealed class MeterTrack : PartBase<EmptyState>
{
    // 1. constants
    private const string DefaultTag = "div";

    // 5. public properties (protected members order as public: the base's hooks the part fills in)
    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;
}
