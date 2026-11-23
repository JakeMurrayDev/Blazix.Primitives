using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// TLB-STA-01; toolbar/separator/ToolbarSeparator.tsx:25 (Separator's state). ADR 08 §3: the state record is sealed, partial
// and [PartState]; the default projection gives data-orientation (TLB-DOM-10).
/// <summary>Represents the state of a toolbar separator, which the part exposes as the <c>data-orientation</c> attribute.</summary>
/// <param name="Orientation">The separator's resolved orientation: perpendicular to the toolbar's unless supplied.</param>
[PartState]
public sealed partial record ToolbarSeparatorState(Orientation Orientation);

// TLB §3.6; toolbar/separator/ToolbarSeparatorDataAttributes.ts:5.
/// <summary>Provides the names of the data attributes a toolbar separator renders.</summary>
public static class ToolbarSeparatorDataAttributes
{
    /// <summary>Represents the attribute that holds the separator's orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's Toolbar.Separator (toolbar/separator/ToolbarSeparator.tsx), which renders Base UI's Separator with a computed
// default orientation; the contract is docs/components/toolbar.md.
// - Its own part over SeparatorAttributes, the helper Separator uses, for role="separator" and aria-orientation (TLB-DOM-10,
//   TLB-ARIA-07, TLB-API-18; chosen by the user 2026-10-26: ADR 16 rule 10 and ADR 08 Amendments 2026-10-22 rule out an
//   internal Separator or a derived part). Structural: not an item (TLB-STR-03), no handlers, no id, no tabindex.
// - Orientation is nullable (ADR 08 Amendments 2026-10-26, chosen by the user): null is perpendicular to the toolbar, a
//   supplied value wins unvalidated (TLB-API-17, TLB-EDGE-08).
/// <summary>
/// Represents a divider between groups of toolbar items: Base UI's <c>Toolbar.Separator</c>, rendered as a <c>div</c>
/// that assistive technology announces as a <c>separator</c>, perpendicular to the toolbar unless its orientation is
/// supplied.
/// </summary>
/// <remarks>The separator is not a stop of the toolbar's arrow keys.</remarks>
public sealed class ToolbarSeparator : PartBase<ToolbarSeparatorState>
{
    private const string DefaultTag = "div";

    private readonly Action toolbarChanged;
    private ToolbarRootContext? toolbar;
    private ToolbarSeparatorState? state;

    /// <summary>Initializes a new instance of the <see cref="ToolbarSeparator"/> class.</summary>
    public ToolbarSeparator()
    {
        toolbarChanged = OnToolbarChanged;
    }

    // TLB-API-17. `Orientation` names the nullable parameter here, so the enum's members are qualified.
    private Orientation EffectiveOrientation => Orientation
        ?? (toolbar!.Orientation == Blazix.Primitives.Orientation.Vertical ? Blazix.Primitives.Orientation.Horizontal : Blazix.Primitives.Orientation.Vertical);

    [CascadingParameter]
    private ToolbarRootContext? ToolbarContext { get; set; }

    // TLB-API-17, TLB-EDGE-08; ADR 08 Amendments 2026-10-26
    /// <summary>Gets or sets the orientation of the separator.</summary>
    /// <value>
    /// The orientation, reflected in <c>aria-orientation</c> and <c>data-orientation</c>; <see langword="null"/>, the
    /// default, for the orientation perpendicular to the toolbar's: <see cref="Blazix.Primitives.Orientation.Vertical"/> in
    /// a horizontal toolbar and <see cref="Blazix.Primitives.Orientation.Horizontal"/> in a vertical one.
    /// </value>
    [Parameter]
    public Orientation? Orientation { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ToolbarSeparatorState State => state!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        toolbar = ToolbarRootContext.Require(ToolbarContext);
        base.OnInitialized();
        toolbar.AddListener(toolbarChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => UpdateState();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            toolbar?.RemoveListener(toolbarChanged);
        }

        base.Dispose(disposing);
    }

    // TLB-DOM-10, TLB-ARIA-07: the role and aria-orientation of the resolved orientation, merged before the consumer's.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => SeparatorAttributes.Write(attributes, state!.Orientation);

    // A change of the toolbar's state: render only when the resolved orientation changed.
    private void OnToolbarChanged()
    {
        if (UpdateState())
        {
            StateHasChanged();
        }
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        Orientation orientation = EffectiveOrientation;
        if (state is { } current && current.Orientation == orientation)
        {
            return false;
        }

        state = new ToolbarSeparatorState(orientation);
        return true;
    }
}
