using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Menu.GroupLabel (menu/group-label/MenuGroupLabel.tsx); the contract is docs/components/menu.md §3.16 (MNU-API-48,
// DOM-19, ARIA-11). A shell over MenuGroupLabelBehavior, which ContextMenu's group label composes too.
/// <summary>Represents the label of a menu group, Base UI's <c>Menu.GroupLabel</c>, rendered as a <c>div</c>.</summary>
/// <remarks>Place it inside a <see cref="MenuGroup"/> or a <see cref="MenuRadioGroup{TValue}"/>.</remarks>
public sealed class MenuGroupLabel : PartBase<EmptyState>
{
    private readonly MenuGroupLabelBehavior behavior = new("Menu");

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
