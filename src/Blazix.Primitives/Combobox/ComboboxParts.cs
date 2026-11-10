using Blazix.Primitives.Rendering;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// CMB-STR-02, STR-04 (combobox/root/ComboboxRootContext.tsx:30-58, the part-local contexts): the missing-context errors,
// thrown in every build with the `Blazix: ` prefix and the family's part names (ADR 08 Amendments 2026-11-03 (3); ADR 02
// Amendments 2026-10-20).
/// <summary>The diagnostics the combobox-family parts share.</summary>
internal static class ComboboxParts
{
    /// <summary>Returns the cascaded store, or throws the missing-root error.</summary>
    /// <param name="store">The cascaded store, or <see langword="null"/>.</param>
    /// <param name="family">The family whose parts the error names.</param>
    /// <returns>The store.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="store"/> is <see langword="null"/>.</exception>
    public static ComboboxStore RequireRoot(ComboboxStore? store, string family) =>
        store ?? throw new InvalidOperationException($"Blazix: `{family}RootContext` is missing. {family} parts must be placed within `<{family}Root>`.");

    /// <summary>Returns the cascaded store of the expected value type, or throws the missing-root error.</summary>
    /// <typeparam name="TValue">The type of the part's value.</typeparam>
    /// <param name="store">The cascaded store, or <see langword="null"/>.</param>
    /// <param name="family">The family whose parts the error names.</param>
    /// <param name="part">The part's name without the family, such as <c>Item</c>.</param>
    /// <returns>The store.</returns>
    /// <exception cref="InvalidOperationException">The store is missing or holds another value type.</exception>
    public static ComboboxStore<TValue> RequireRoot<TValue>(ComboboxStore? store, string family, string part) => RequireRoot(store, family) switch
    {
        ComboboxStore<TValue> typed => typed,
        _ => throw new InvalidOperationException($"Blazix: `{family}{part}` has a value type that differs from its `<{family}Root>`'s. Write `TValue` on the part, or give it a value of the root's exact type."),
    };

    /// <summary>Gets the error of a positioner outside a portal.</summary>
    /// <param name="family">The family whose parts the error names.</param>
    /// <returns>The error.</returns>
    public static InvalidOperationException MissingPortal(string family) => new($"Blazix: `<{family}Portal>` is missing.");

    /// <summary>Gets the error of a popup or arrow outside a positioner.</summary>
    /// <param name="family">The family whose parts the error names.</param>
    /// <returns>The error.</returns>
    public static InvalidOperationException MissingPositioner(string family) =>
        new($"Blazix: `<{family}Popup>` and `<{family}Arrow>` must be used within the `<{family}Positioner>` component.");

    /// <summary>Gets the error of an item part outside an item.</summary>
    /// <param name="family">The family whose parts the error names.</param>
    /// <returns>The error.</returns>
    public static InvalidOperationException MissingItem(string family) =>
        new($"Blazix: `{family}ItemContext` is missing. `{family}Item` parts must be placed within `<{family}Item>`.");

    /// <summary>Gets the error of a chip part outside a chip.</summary>
    /// <param name="family">The family whose parts the error names.</param>
    /// <returns>The error.</returns>
    public static InvalidOperationException MissingChip(string family) =>
        new($"Blazix: `{family}ChipContext` is missing. `{family}Chip` parts must be placed within `<{family}Chip>`.");

    /// <summary>Gets the error of a chip outside a chips container.</summary>
    /// <param name="family">The family whose parts the error names.</param>
    /// <returns>The error.</returns>
    public static InvalidOperationException MissingChips(string family) =>
        new($"Blazix: `{family}ChipsContext` is missing. `{family}Chip` must be placed within `<{family}Chips>`.");

    /// <summary>Gets the error of a group label outside a group.</summary>
    /// <param name="family">The family whose parts the error names.</param>
    /// <returns>The error.</returns>
    public static InvalidOperationException MissingGroup(string family) =>
        new($"Blazix: `{family}GroupContext` is missing. `{family}Group` parts must be placed within `<{family}Group>`.");

    // useFieldValidation.ts:367-376 (getValidationProps), applied after the merge as the field's controls do: the consumer's
    // description ids composed with the field's, and aria-invalid while the field is invalid and neither it nor the part is
    // disabled (ComboboxTrigger.tsx:258; ComboboxInput.tsx:127-129 outside the popup).
    /// <summary>Sets the field's validation attributes over the merged ones.</summary>
    /// <param name="attributes">The merged attributes.</param>
    /// <param name="field">The enclosing field, or <see langword="null"/>.</param>
    /// <param name="disabled">Whether the part is disabled.</param>
    public static void AddValidationAttributes(AttributeList attributes, FieldRootContext? field, bool disabled)
    {
        if (field is null)
        {
            return;
        }

        string? consumer = attributes.TryGetValue("aria-describedby", out AttributeValue current) && current.TryGetValue(out string text) ? text : null;
        if (field.Labelable.DescribedBy(consumer) is { } describedBy)
        {
            attributes.Set("aria-describedby", describedBy);
        }
        else
        {
            attributes.Remove("aria-describedby");
        }

        if (field.State.Valid == false && !field.Disabled && !disabled)
        {
            attributes.Set("aria-invalid", "true");
        }
    }
}

// combobox/utils/ComboboxInternalDismissButton.tsx (CMB-STR-07, DOM-22): a visually hidden non-native button with no
// tabindex, rendered inline by the Input (before) and the Popup (after) (ADR 16 rule 10: no part); the `combobox` module
// listens on it and reports a press as `close-press` (the rule of ADR 04 Amendments 2026-10-27 that C# renders an inner
// element and the owning module listens).
/// <summary>Renders a combobox's internal dismiss button.</summary>
internal static class ComboboxDismissButton
{
    /// <summary>Renders the button.</summary>
    /// <param name="builder">The owning part's render tree builder.</param>
    /// <param name="id">The button's id, which the focus manager treats as inside the popup.</param>
    public static void Build(RenderTreeBuilder builder, string id)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "id", id);
        builder.AddAttribute(2, "role", "button");
        builder.AddAttribute(3, "aria-label", "Dismiss");
        builder.AddAttribute(4, "style", VisuallyHidden.InputStyle);
        builder.CloseElement();
    }
}
