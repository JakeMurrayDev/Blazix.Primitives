using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's ContextMenu.GroupLabel is Menu.GroupLabel (context-menu/index.parts.ts:4-19; CTX-STR-01, STR-02: the MNU rows below are
// adopted). Decision 12 of the M5 plan and ADR 08 Amendments 2026-11-03 (3) (chosen by the user 2026-11-03): a sealed shell
// over Menu's behaviour with the family name `ContextMenu`, which its diagnostics name (CTX-EDGE-02); M5 track 7.
// Base UI's Menu.GroupLabel (menu/group-label/MenuGroupLabel.tsx); the contract is docs/components/menu.md §3.16 (MNU-API-48,
// DOM-19, ARIA-11). A shell over MenuGroupLabelBehavior, which ContextMenu's group label composes too.
/// <summary>Represents the label of a menu group, Base UI's <c>ContextMenu.GroupLabel</c>, rendered as a <c>div</c>.</summary>
/// <remarks>Place it inside a <see cref="ContextMenuGroup"/> or a <see cref="ContextMenuRadioGroup{TValue}"/>.</remarks>
public sealed class ContextMenuGroupLabel : PartBase<EmptyState>
{
    private readonly MenuGroupLabelBehavior behavior = new("ContextMenu");

    [CascadingParameter]
    private MenuGroupContext? Group { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The label is outside a group.</exception>
    protected override void OnInitialized()
    {
        behavior.Subscribe(Group);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior.Apply(Id);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior.AddAttributes(attributes);
}
