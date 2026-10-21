using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// ADR 08 §3: the state record is sealed, partial and [PartState]. SEP-STA-01: exactly one field. SEP-DOM-01: the
// generator's default projection writes data-orientation, so the part declares no custom mapping.
/// <summary>Represents the state of a <see cref="Separator"/>, which the part exposes as the <c>data-orientation</c> attribute.</summary>
/// <param name="Orientation">The effective orientation of the separator.</param>
[PartState]
public sealed partial record SeparatorState(Orientation Orientation);

// Base UI's Separator; the contract is docs/components/separator.md (SEP-STR-01, SEP-API-01..03, SEP-DOM-02, SEP-ARIA-01/02,
// SEP-FOC-01). The part is structural: no handlers, no id, no tabindex, no store.
/// <summary>
/// Represents a static divider between groups of content: Base UI's <c>Separator</c>, rendered as a <c>div</c> that
/// assistive technology announces as a <c>separator</c>.
/// </summary>
/// <remarks>
/// The part contributes no visuals and no interaction. It renders <c>role="separator"</c>, an <c>aria-orientation</c> and a
/// <c>data-orientation</c> attribute (the latter two for both orientations), and never a <c>tabindex</c>. An attribute the
/// consumer supplies, such as a different <c>role</c>, replaces the built-in one.
/// </remarks>
public sealed class Separator : PartBase<SeparatorState>
{
    // 1. constants
    private const string DefaultTag = "div";
    private const string HorizontalLiteral = "horizontal";
    private const string VerticalLiteral = "vertical";

    // 2. fields: the state record is reused while the orientation is unchanged (ADR 16 rule 5 memoises on its identity)
    private SeparatorState? state;

    // 4. parameter properties
    /// <summary>Gets or sets the orientation of the separator.</summary>
    /// <value>The orientation, reflected in <c>aria-orientation</c> and <c>data-orientation</c>. The default is <see cref="Blazix.Primitives.Orientation.Horizontal"/>.</value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    // 5. public properties (protected members order as public: the base's hooks the part fills in)
    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override SeparatorState State
    {
        get
        {
            if (state is null || state.Orientation != Orientation)
            {
                state = new SeparatorState(Orientation);
            }

            return state;
        }
    }

    // 9. public methods (protected ones too)
    // SEP-ARIA-01/02: the role and aria-orientation are part-supplied, merged before the consumer's attributes (SEP-DOM-02).
    // aria-orientation is written for horizontal too (SEP-ARIA-02). A value outside the two members (SEP-EDGE-01 has no
    // reachable case in a typed port) reads as horizontal here, and the projection writes no data-orientation for it.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "separator");
        attributes.Set("aria-orientation", Orientation == Orientation.Vertical ? VerticalLiteral : HorizontalLiteral);
    }
}
