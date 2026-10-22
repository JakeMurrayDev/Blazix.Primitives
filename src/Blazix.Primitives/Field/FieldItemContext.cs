namespace Blazix.Primitives;

// FLD-STR-04/06, FLD-API-27, FFI-STR-03, FFI-STA-11; field/item/FieldItem.tsx:29-47, field/item/FieldItemContext.ts. The item
// scope: the item's disabled state and the nested labelable scope it opens. A part uses an item only when the item
// belongs to the part's nearest field, so a FieldRoot nested inside another field's item opens fresh scopes (FLD-EDGE-11).
/// <summary>The item scope of one <see cref="FieldItem"/>: its disabled state and the labelable scope it opens.</summary>
internal sealed class FieldItemContext
{
    private bool disabledParameter;

    /// <summary>Initializes a new instance of the <see cref="FieldItemContext"/> class.</summary>
    /// <param name="field">The field the item belongs to.</param>
    /// <param name="defaultControlId">The generated control id the item's labelable scope starts with.</param>
    /// <param name="parent">The enclosing labelable scope, whose description ids come first.</param>
    public FieldItemContext(FieldRootContext field, string defaultControlId, LabelableScope parent)
    {
        Field = field;
        Labelable = new LabelableScope(defaultControlId, parent, field.NotifyChanged);
    }

    /// <summary>Gets the field the item belongs to.</summary>
    /// <value>The field scope.</value>
    public FieldRootContext Field { get; }

    /// <summary>Gets the labelable scope the item opens.</summary>
    /// <value>The scope of the item's label, control and descriptions.</value>
    public LabelableScope Labelable { get; }

    // FLD-API-27: `rootDisabled || disabled`.
    /// <summary>Gets a value indicating whether the item is disabled, by its own parameter or its field.</summary>
    /// <value>The effective disabled state.</value>
    public bool Disabled => Field.Disabled || disabledParameter;

    /// <summary>Resolves the labelable scope of a part.</summary>
    /// <param name="field">The part's nearest field.</param>
    /// <param name="item">The part's nearest item, or <see langword="null"/>.</param>
    /// <returns>The item's scope when the item belongs to <paramref name="field"/>; otherwise, the field's.</returns>
    public static LabelableScope LabelableOf(FieldRootContext field, FieldItemContext? item) => item is not null && ReferenceEquals(item.Field, field) ? item.Labelable : field.Labelable;

    // FLD-STA-02/04: a label or a description reflects `rootDisabled || itemDisabled`.
    /// <summary>Resolves the disabled state of a label or a description.</summary>
    /// <param name="field">The part's nearest field.</param>
    /// <param name="item">The part's nearest item, or <see langword="null"/>.</param>
    /// <returns>Whether the field, or an item of the field, is disabled.</returns>
    public static bool DisabledOf(FieldRootContext field, FieldItemContext? item) => item is not null && ReferenceEquals(item.Field, field) ? item.Disabled : field.Disabled;

    /// <summary>Sets the item's own disabled parameter.</summary>
    /// <param name="disabled">The disabled parameter.</param>
    public void SetDisabled(bool disabled)
    {
        if (disabledParameter == disabled)
        {
            return;
        }

        disabledParameter = disabled;
        Field.NotifyChanged();
    }
}
