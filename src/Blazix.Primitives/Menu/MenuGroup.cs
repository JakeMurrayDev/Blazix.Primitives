using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Menu.Group (menu/group/MenuGroup.tsx); the contract is docs/components/menu.md §3.16 (MNU-ARIA-10): role="group",
// aria-labelledby = the mounted label's id (MenuGroupContext, the PopoverIdSync form).
/// <summary>Groups related items of a menu, Base UI's <c>Menu.Group</c>, rendered as a <c>div</c> with the <c>group</c> role.</summary>
public sealed class MenuGroup : PartBase<EmptyState>
{
    private readonly MenuGroupContext group = new();
    private IDisposable? subscription;

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
        MenuRootContext.Require(RootContext, "Menu");
        subscription = group.Subscribe(StateHasChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => ChildContent = group.Cascade(ChildContent);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscription?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "group");
        if (group.LabelId is { } labelId)
        {
            attributes.Set("aria-labelledby", labelId);
        }
    }
}
