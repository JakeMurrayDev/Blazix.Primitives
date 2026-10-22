using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// FLD-STA-05; field/item/FieldItem.tsx:29-32,50.
/// <summary>Represents the state of a field item, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the item is disabled, by its own parameter or its field; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether the field has been touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the field's value differs from its initial value; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's verdict: projected as <c>data-valid</c> for <see langword="true"/>, <c>data-invalid</c> for <see langword="false"/>, and neither for <see langword="null"/>.</param>
/// <param name="Filled">A value indicating whether the field's control has a value; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the field's control has focus; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record FieldItemState(bool Disabled, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused);

// FLD-DOM-04; field/item/FieldItemDataAttributes.ts.
/// <summary>Provides the names of the data attributes a field item renders.</summary>
public static class FieldItemDataAttributes
{
    /// <summary>Represents the attribute present while the item is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present while the field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present once the field has been touched: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present while the field's value differs from its initial value: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present while the field has a value: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present while the field's control has focus: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

// Base UI's Field.Item (field/item/FieldItem.tsx); the contract is docs/components/field.md. FLD-STR-04/06, FFI-STR-03: it
// opens a nested labelable scope, cascaded with its disabled state around its element (the contexts design point, pending
// the user); it attaches no handler, role or id of its own. FLD-API-27: Disabled never reaches the element.
/// <summary>
/// Represents one option of a checkbox group or a radio group inside a field: Base UI's <c>Field.Item</c>, rendered as a
/// <c>div</c>. A <see cref="FieldLabel"/> and a <see cref="FieldDescription"/> inside it label and describe the control
/// inside it.
/// </summary>
public sealed class FieldItem : PartBase<FieldItemState>
{
    private const string DefaultTag = "div";

    private static readonly StateAttributesMapping<FieldItemState> s_mapping = FieldValidityMapping.Create<FieldItemState>();

    private readonly RenderFragment renderPart;
    private readonly Action fieldChanged;
    private FieldRootContext? field;
    private FieldItemContext? item;
    private FieldItemState? state;
    private FieldStateSnapshot renderedSnapshot;

    /// <summary>Initializes a new instance of the <see cref="FieldItem"/> class.</summary>
    public FieldItem()
    {
        renderPart = RenderPart;
        fieldChanged = OnFieldChanged;
    }

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private FieldItemContext? OuterItem { get; set; }

    // FLD-API-27
    /// <summary>Gets or sets a value indicating whether the control inside the item ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable the item, its label, its description and the control inside it; otherwise,
    /// <see langword="false"/>, the default. A disabled field disables the item whatever this value is.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override FieldItemState State
    {
        get
        {
            FieldStateSnapshot snapshot = Snapshot();
            if (state is null || snapshot != renderedSnapshot)
            {
                renderedSnapshot = snapshot;
                state = new FieldItemState(snapshot.Disabled, snapshot.Touched, snapshot.Dirty, snapshot.Valid, snapshot.Filled, snapshot.Focused);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<FieldItemState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The item is not inside a <see cref="FieldRoot"/>.</exception>
    protected override void OnInitialized()
    {
        field = FieldScopes.RequireField(FieldContext);
        item = new FieldItemContext(field, IdGenerator.Next(), FieldItemContext.LabelableOf(field, OuterItem));
        field.AddListener(fieldChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        item!.SetDisabled(Disabled);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<FieldItemContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<FieldItemContext>.Value), item);
        builder.AddComponentParameter(2, nameof(CascadingValue<FieldItemContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<FieldItemContext>.ChildContent), renderPart);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            field?.RemoveListener(fieldChanged);
        }

        base.Dispose(disposing);
    }

    private FieldStateSnapshot Snapshot() => field!.State with { Disabled = item!.Disabled };

    private void RenderPart(RenderTreeBuilder builder) => base.BuildRenderTree(builder);

    private void OnFieldChanged()
    {
        if (state is not null && Snapshot() == renderedSnapshot)
        {
            return;
        }

        StateHasChanged();
    }
}
