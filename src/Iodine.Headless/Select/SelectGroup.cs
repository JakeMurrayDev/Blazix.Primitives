using Blazix.Primitives.Rendering;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// SEL §11 (SelectGroupContext); select/group/SelectGroupContext.ts. The group's label id, which its GroupLabel publishes
// and withdraws with FFI-ARIA-08's semantics (an older label's withdrawal never clears a newer id; LabelIdSlot).
/// <summary>The state a select group shares with its label.</summary>
internal sealed class SelectGroupContext
{
    /// <summary>Gets the slot of the group label's id.</summary>
    public LabelIdSlot Label { get; } = new();

    /// <summary>Throws the error of a group label rendered outside a select group.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static SelectGroupContext Require(SelectGroupContext? context)
    {
        // SEL-EDGE-01, SEL-STR-05.
        return context ?? throw new InvalidOperationException(
            "Blazix: `SelectGroupContext` is missing. SelectGroup parts must be placed within `<SelectGroup>`.");
    }
}

// Base UI's Select.Group (select/group/SelectGroup.tsx); SEL-DOM-41, SEL-ARIA-11: a `div` with `role="group"` and
// `aria-labelledby` while a GroupLabel is mounted; the context is cascaded fixed and the slot renders the group again.
/// <summary>Represents a group of related items in a select's list, Base UI's <c>Select.Group</c>, rendered as a <c>div</c>.</summary>
public sealed class SelectGroup : PartBase<EmptyState>
{
    private readonly SelectGroupContext context = new();
    private readonly RenderFragment cascadedContent;
    private RenderFragment? consumerContent;

    /// <summary>Initializes a new instance of the <see cref="SelectGroup"/> class.</summary>
    public SelectGroup()
    {
        cascadedContent = RenderCascade;
        context.Label.Changed = () => _ = InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        consumerContent = ChildContent;
        ChildContent = cascadedContent;
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "group");
        if (context.Label.Id is { } labelId)
        {
            attributes.Set("aria-labelledby", labelId);
        }
    }

    private void RenderCascade(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<SelectGroupContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<SelectGroupContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<SelectGroupContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<SelectGroupContext>.ChildContent), consumerContent);
        builder.CloseComponent();
    }
}

// Base UI's Select.GroupLabel (select/group-label/SelectGroupLabel.tsx); SEL-API-46, SEL-DOM-42, SEL-ARIA-12, SEL-EDGE-18:
// the consumer's id or a generated one, published to the group in the parameter set (ADR 01 Amendments 2026-10-22) and
// withdrawn when it goes away, and `aria-hidden="true"` unless the consumer gives one.
/// <summary>Represents the label of a select group, Base UI's <c>Select.GroupLabel</c>, rendered as a <c>div</c>.</summary>
/// <remarks>It is hidden from assistive technology; the group's accessible name still comes from it.</remarks>
public sealed class SelectGroupLabel : PartBase<EmptyState>
{
    private SelectGroupContext? group;
    private string? publishedId;

    [CascadingParameter]
    private SelectGroupContext? GroupContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The label is not inside a <see cref="SelectGroup"/>.</exception>
    protected override void OnInitialized()
    {
        group = SelectGroupContext.Require(GroupContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        string? id = Enabled ? Id : null;
        if (string.Equals(id, publishedId, StringComparison.Ordinal))
        {
            return;
        }

        if (id is not null)
        {
            group!.Label.Publish(id);
        }
        else if (publishedId is not null)
        {
            group!.Label.Withdraw(publishedId);
        }

        publishedId = id;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && publishedId is not null)
        {
            group!.Label.Withdraw(publishedId);
            publishedId = null;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set("aria-hidden", "true");
}

// SEL-STA-26 (select/separator/SelectSeparator.tsx:14-19).
/// <summary>Represents the state of a select separator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Orientation">The separator's orientation; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record SelectSeparatorState(Orientation Orientation);

// select/separator/SelectSeparatorDataAttributes.ts (SEL-DOM-46).
/// <summary>Provides the names of the data attributes a select separator renders.</summary>
public static class SelectSeparatorDataAttributes
{
    /// <summary>Represents the attribute whose value is the separator's orientation: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's Select.Separator (select/separator/SelectSeparator.tsx over utils/listbox-separator/ListboxSeparator.tsx);
// SEL-API-48, SEL-DOM-46, SEL-ARIA-15: track 4's ListboxSeparatorAttributes (RND-STR-11) and the orientation from the state.
/// <summary>Represents a separator between the items of a select's list, Base UI's <c>Select.Separator</c>, rendered as a <c>div</c>.</summary>
public sealed class SelectSeparator : PartBase<SelectSeparatorState>
{
    private static readonly SelectSeparatorState s_horizontal = new(Orientation.Horizontal);
    private static readonly SelectSeparatorState s_vertical = new(Orientation.Vertical);

    /// <summary>Gets or sets the orientation of the separator.</summary>
    /// <value><see cref="Orientation.Horizontal"/> by default.</value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override SelectSeparatorState State => Orientation == Orientation.Vertical ? s_vertical : s_horizontal;

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => ListboxSeparatorAttributes.Write(attributes);
}
