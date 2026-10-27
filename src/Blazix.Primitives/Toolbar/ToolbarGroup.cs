using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// TLB-STA-01; toolbar/group/ToolbarGroup.tsx:38-41,54. ADR 08 §3: the state record is sealed, partial and [PartState]; the
// default projection gives data-disabled and data-orientation (TLB-DOM-09).
/// <summary>Represents the state of a toolbar group, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the group's buttons and inputs ignore user interaction; projected as <c>data-disabled</c>.</param>
/// <param name="Orientation">The toolbar's orientation; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record ToolbarGroupState(bool Disabled, Orientation Orientation);

// TLB §3.5; toolbar/group/ToolbarGroupDataAttributes.ts:4,9, in its order.
/// <summary>Provides the names of the data attributes a toolbar group renders.</summary>
public static class ToolbarGroupDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the group is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute that holds the toolbar's orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's Toolbar.Group (toolbar/group/ToolbarGroup.tsx); the contract is docs/components/toolbar.md.
// - One `div` with role="group" (TLB-ARIA-06, TLB-DOM-09); the consumer's attributes win (TLB-API-16). Structural: not an
//   item, no handlers, no id, no tabindex (TLB-STR-03).
// - Its effective disabled state is the toolbar's or its own (TLB-API-15), published to the parts inside it through
//   ToolbarGroupContext (design point 14.4 (a), pending the user); links do not read it.
/// <summary>
/// Represents a set of related toolbar items: Base UI's <c>Toolbar.Group</c>, rendered as a <c>div</c> with
/// <c>role="group"</c>. Disabling the group disables its buttons, inputs and toggle groups.
/// </summary>
/// <remarks>
/// The group is not a stop of the toolbar's arrow keys; its items are. Give it an accessible name with an
/// <c>aria-label</c> or <c>aria-labelledby</c> attribute.
/// </remarks>
public sealed class ToolbarGroup : PartBase<ToolbarGroupState>
{
    private const string DefaultTag = "div";

    private readonly Action toolbarChanged;
    private ToolbarRootContext? toolbar;
    private ToolbarGroupContext? context;
    private ToolbarGroupState? state;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;

    /// <summary>Initializes a new instance of the <see cref="ToolbarGroup"/> class.</summary>
    public ToolbarGroup()
    {
        toolbarChanged = OnToolbarChanged;
    }

    // TLB-API-15
    private bool EffectiveDisabled => toolbar!.Disabled || Disabled;

    [CascadingParameter]
    private ToolbarRootContext? ToolbarContext { get; set; }

    // TLB-API-15
    /// <summary>Gets or sets a value indicating whether the group's buttons and inputs ignore user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable every button, input and toggle group in the group, whatever their own setting;
    /// links are unaffected. The default is <see langword="false"/>; a disabled toolbar disables the group either way.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ToolbarGroupState State => state!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        toolbar = ToolbarRootContext.Require(ToolbarContext);
        base.OnInitialized();
        context = new ToolbarGroupContext(EffectiveDisabled);
        toolbar.AddListener(toolbarChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        UpdateState();
        context!.SetState(EffectiveDisabled);

        // The group's context wraps the consumer's content; rebuilt only when the consumer supplied other content.
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            toolbar?.RemoveListener(toolbarChanged);
        }

        base.Dispose(disposing);
    }

    // TLB-ARIA-06: role="group"; the consumer's role wins at the merge (TLB-API-16).
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "group");
    }

    // A change of the toolbar's state: publish the group's disabled state and render only when the group's state changed.
    private void OnToolbarChanged()
    {
        context!.SetState(EffectiveDisabled);
        if (UpdateState())
        {
            StateHasChanged();
        }
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        bool disabled = EffectiveDisabled;
        Orientation orientation = toolbar!.Orientation;
        if (state is { } current && current.Disabled == disabled && current.Orientation == orientation)
        {
            return false;
        }

        state = new ToolbarGroupState(disabled, orientation);
        return true;
    }
}
