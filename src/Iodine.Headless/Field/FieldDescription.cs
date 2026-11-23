using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// FLD-STA-04; field/description/FieldDescription.tsx:31-34,58.
/// <summary>Represents the state of a field description, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the field, or the item the description is in, is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether the field has been touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the field's value differs from its initial value; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's verdict: projected as <c>data-valid</c> for <see langword="true"/>, <c>data-invalid</c> for <see langword="false"/>, and neither for <see langword="null"/>.</param>
/// <param name="Filled">A value indicating whether the field's control has a value; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the field's control has focus; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record FieldDescriptionState(bool Disabled, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused);

// FLD-DOM-04; field/description/FieldDescriptionDataAttributes.ts.
/// <summary>Provides the names of the data attributes a field description renders.</summary>
public static class FieldDescriptionDataAttributes
{
    /// <summary>Represents the attribute present while the description is disabled: <c>data-disabled</c>.</summary>
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

// Base UI's Field.Description (field/description/FieldDescription.tsx); the contract is docs/components/field.md.
// FLD-API-24, FLD-ARIA-04, FFI-ARIA-02: the id (the consumer's verbatim, even empty, or a generated one) is published to
// the scope's description ids while the part is mounted, in the render batch that creates it (ADR 01 Amendments
// 2026-10-22); an empty id is never published (FLD-EDGE-03).
/// <summary>
/// Represents a paragraph that describes a field's control: Base UI's <c>Field.Description</c>, rendered as a <c>p</c>. It
/// belongs inside a <see cref="FieldRoot"/>; the control lists its id in <c>aria-describedby</c>.
/// </summary>
public sealed class FieldDescription : PartBase<FieldDescriptionState>
{
    private const string DefaultTag = "p";

    private static readonly StateAttributesMapping<FieldDescriptionState> s_mapping = FieldValidityMapping.Create<FieldDescriptionState>();

    private readonly Action fieldChanged;
    private FieldRootContext? field;
    private FieldItemContext? item;
    private LabelableScope? scope;
    private string? publishedId;
    private FieldDescriptionState? state;
    private FieldStateSnapshot renderedSnapshot;

    /// <summary>Initializes a new instance of the <see cref="FieldDescription"/> class.</summary>
    public FieldDescription()
    {
        fieldChanged = OnFieldChanged;
    }

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private FieldItemContext? ItemContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override FieldDescriptionState State
    {
        get
        {
            FieldStateSnapshot snapshot = Snapshot();
            if (state is null || snapshot != renderedSnapshot)
            {
                renderedSnapshot = snapshot;
                state = new FieldDescriptionState(snapshot.Disabled, snapshot.Touched, snapshot.Dirty, snapshot.Valid, snapshot.Filled, snapshot.Focused);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<FieldDescriptionState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The description is not inside a <see cref="FieldRoot"/>.</exception>
    protected override void OnInitialized()
    {
        field = FieldScopes.RequireField(FieldContext);
        item = ItemContext;
        scope = FieldItemContext.LabelableOf(field, item);
        field.AddListener(fieldChanged);
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

        using FieldRootContext.BatchScope batch = field!.Batch();
        if (publishedId is not null)
        {
            scope!.RemoveMessageId(publishedId);
        }

        if (id is not null)
        {
            scope!.AddMessageId(id);
        }

        publishedId = id;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && field is not null)
        {
            field.RemoveListener(fieldChanged);
            if (publishedId is not null)
            {
                scope!.RemoveMessageId(publishedId);
                publishedId = null;
            }
        }

        base.Dispose(disposing);
    }

    private FieldStateSnapshot Snapshot() => field!.State with { Disabled = FieldItemContext.DisabledOf(field, item) };

    private void OnFieldChanged()
    {
        if (state is not null && Snapshot() == renderedSnapshot)
        {
            return;
        }

        StateHasChanged();
    }
}
