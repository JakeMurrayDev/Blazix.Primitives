using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Menu.GroupLabel (menu/group-label/MenuGroupLabel.tsx); the contract is docs/components/menu.md §3.16 (MNU-API-48,
// DOM-19, ARIA-11): its id, given or generated, labels the enclosing group while it is mounted; aria-hidden="true" by default,
// which a consumer's attribute overrides.
/// <summary>Represents the label of a menu group, Base UI's <c>Menu.GroupLabel</c>, rendered as a <c>div</c>.</summary>
/// <remarks>Place it inside a <see cref="MenuGroup"/> or a <see cref="MenuRadioGroup{TValue}"/>.</remarks>
public sealed class MenuGroupLabel : PartBase<EmptyState>
{
    private string? registeredId;

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
        if (Group is null)
        {
            throw MenuParts.MissingGroup("Menu");
        }

        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // MenuGroupLabel.tsx:25-30: an effect on the id, so a render of the group that changes nothing sets nothing, and two
        // mounted labels do not take the group's label from each other.
        if (string.Equals(registeredId, Id, StringComparison.Ordinal))
        {
            return;
        }

        if (registeredId is not null)
        {
            Group!.SetLabelId(null, registeredId);
        }

        registeredId = Id;
        Group!.SetLabelId(Id);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Group?.SetLabelId(null, registeredId);
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set("aria-hidden", "true");
}
