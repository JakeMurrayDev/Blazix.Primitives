using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Menu.Group (menu/group/MenuGroup.tsx); the contract is docs/components/menu.md §3.16 (MNU-ARIA-10). A shell over
// MenuGroupBehavior, which ContextMenu's group composes too.
/// <summary>Groups related items of a menu, Base UI's <c>Menu.Group</c>, rendered as a <c>div</c> with the <c>group</c> role.</summary>
public sealed class MenuGroup : PartBase<EmptyState>
{
    private MenuGroupBehavior? behavior;
    private MenuFilterGroupContext? filterGroup;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private MenuFilterGroupContext? FilterGroup { get; set; }

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
        behavior = new MenuGroupBehavior("Menu", StateHasChanged);
        behavior.Subscribe(RootContext);

        // MNU-STA-24, DOM-20 (M5 track 12): in a filterable menu the group counts its members and hides with them.
        if (RootContext!.Filter is not null)
        {
            filterGroup = new MenuFilterGroupContext(FilterGroup, () => _ = InvokeAsync(StateHasChanged));
        }

        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ChildContent = behavior!.Cascade(ChildContent);
        if (filterGroup is not null)
        {
            ChildContent = filterGroup.Cascade(ChildContent);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
            filterGroup?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes);

        // MNU-DOM-20: the boolean hidden while the query hides every member; a consumer's hidden is kept.
        if (filterGroup is { Hidden: true })
        {
            attributes.Set("hidden", true);
        }
    }
}
