using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's ContextMenu.Group is Menu.Group (context-menu/index.parts.ts:4-19; CTX-STR-01, STR-02: the MNU rows below are
// adopted). Decision 12 of the M5 plan and ADR 08 Amendments 2026-11-03 (3) (chosen by the user 2026-11-03): a sealed shell
// over Menu's behaviour with the family name `ContextMenu`, which its diagnostics name (CTX-EDGE-02); M5 track 7.
// Base UI's Menu.Group (menu/group/MenuGroup.tsx); the contract is docs/components/menu.md §3.16 (MNU-ARIA-10). A shell over
// MenuGroupBehavior, which ContextMenu's group composes too.
/// <summary>Groups related items of a context menu, Base UI's <c>ContextMenu.Group</c>, rendered as a <c>div</c> with the <c>group</c> role.</summary>
public sealed class ContextMenuGroup : PartBase<EmptyState>
{
    private MenuGroupBehavior? behavior;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The group is outside a menu.</exception>
    protected override void OnInitialized()
    {
        behavior = new MenuGroupBehavior("ContextMenu", StateHasChanged);
        behavior.Subscribe(RootContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => ChildContent = behavior!.Cascade(ChildContent);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);
}
