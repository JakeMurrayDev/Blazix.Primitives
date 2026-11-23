namespace Blazix.Primitives;

// MNU-EDGE-01, MNU-EDGE-03 (menu/root/MenuRootContext.ts, positioner/MenuPositionerContext.ts, portal/MenuPortalContext.ts,
// group/MenuGroupContext.ts, radio-group/MenuRadioGroupContext.ts, checkbox-item/MenuCheckboxItemContext.ts,
// radio-item/MenuRadioItemContext.ts, submenu-root/MenuSubmenuRootContext.ts, trigger/MenuTrigger.tsx:69-72): Base UI's
// wording with the C# names, built from the family name each behaviour passes (DialogParts' form), so ContextMenu's shells
// name their own parts (ADR 08 Amendments 2026-11-03 (3)). Thrown in every build.
/// <summary>Provides the messages of a menu-family part placed outside the part it needs.</summary>
internal static class MenuParts
{
    /// <summary>Creates the exception of a part rendered outside any root of its family.</summary>
    /// <param name="familyName">The family's name, such as <c>Menu</c>.</param>
    /// <returns>The exception.</returns>
    public static InvalidOperationException MissingRoot(string familyName) =>
        new($"Blazix: The `{familyName}Root` context is missing. {familyName} parts must be placed within `<{familyName}Root>`.");

    /// <summary>Creates the exception of a trigger rendered outside its root without a handle.</summary>
    /// <param name="familyName">The family's name, such as <c>Menu</c>.</param>
    /// <returns>The exception.</returns>
    public static InvalidOperationException MissingTriggerRoot(string familyName) =>
        new($"Blazix: `<{familyName}Trigger>` must be either used within a `<{familyName}Root>` component or provided with a handle.");

    /// <summary>Creates the exception of a part rendered outside its family's positioner.</summary>
    /// <param name="familyName">The family's name, such as <c>Menu</c>.</param>
    /// <returns>The exception.</returns>
    public static InvalidOperationException MissingPositioner(string familyName) =>
        new($"Blazix: The `{familyName}Positioner` context is missing. {familyName}Positioner parts must be placed within `<{familyName}Positioner>`.");

    /// <summary>Creates the exception of a portaled part rendered outside its family's portal.</summary>
    /// <param name="familyName">The family's name, such as <c>Menu</c>.</param>
    /// <returns>The exception.</returns>
    public static InvalidOperationException MissingPortal(string familyName) =>
        new($"Blazix: `<{familyName}Portal>` is missing.");

    /// <summary>Creates the exception of a group label rendered outside a group or a radio group.</summary>
    /// <param name="familyName">The family's name, such as <c>Menu</c>.</param>
    /// <returns>The exception.</returns>
    public static InvalidOperationException MissingGroup(string familyName) =>
        new($"Blazix: The `{familyName}Group` context is missing. {familyName} group parts must be used within `<{familyName}Group>` or `<{familyName}RadioGroup>`.");

    /// <summary>Creates the exception of a radio item rendered outside a radio group.</summary>
    /// <param name="familyName">The family's name, such as <c>Menu</c>.</param>
    /// <returns>The exception.</returns>
    public static InvalidOperationException MissingRadioGroup(string familyName) =>
        new($"Blazix: The `{familyName}RadioGroup` context is missing. {familyName}RadioGroup parts must be placed within `<{familyName}RadioGroup>`.");

    /// <summary>Creates the exception of a checkbox item indicator rendered outside a checkbox item.</summary>
    /// <param name="familyName">The family's name, such as <c>Menu</c>.</param>
    /// <returns>The exception.</returns>
    public static InvalidOperationException MissingCheckboxItem(string familyName) =>
        new($"Blazix: The `{familyName}CheckboxItem` context is missing. {familyName}CheckboxItem parts must be placed within `<{familyName}CheckboxItem>`.");

    /// <summary>Creates the exception of a radio item indicator rendered outside a radio item.</summary>
    /// <param name="familyName">The family's name, such as <c>Menu</c>.</param>
    /// <returns>The exception.</returns>
    public static InvalidOperationException MissingRadioItem(string familyName) =>
        new($"Blazix: The `{familyName}RadioItem` context is missing. {familyName}RadioItem parts must be placed within `<{familyName}RadioItem>`.");

    // MNU-EDGE-04, STR-15 (menu/filter-root/MenuFilterContext.ts:85-96), in the port's names.
    /// <summary>Creates the exception a filter part throws outside a filterable menu.</summary>
    /// <param name="part">The part's name without the family prefix: <c>Input</c>, <c>Clear</c> or <c>Empty</c>.</param>
    /// <returns>The exception.</returns>
    public static InvalidOperationException MissingFilter(string part) =>
        new($"Blazix: `<Menu{part}>` must be placed in a menu wrapped in `<MenuFilterProvider>`. It reads the filter query and the matching items from the provider, which a plain menu does not have. Wrap the `<MenuRoot>` or `<MenuSubmenuRoot>` it belongs to in `<MenuFilterProvider>`.");

    /// <summary>Creates the exception of a submenu trigger rendered outside a submenu root.</summary>
    /// <param name="familyName">The family's name, such as <c>Menu</c>.</param>
    /// <returns>The exception.</returns>
    public static InvalidOperationException MissingSubmenuRoot(string familyName) =>
        new($"Blazix: `<{familyName}SubmenuTrigger>` must be placed in `<{familyName}SubmenuRoot>`.");
}
