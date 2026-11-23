using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// Base UI's Menu.GroupLabel (menu/group-label/MenuGroupLabel.tsx; MNU-API-48, DOM-19, ARIA-11): its id, given or
// generated, labels the enclosing group while it is mounted; aria-hidden="true" by default, which a consumer's attribute
// overrides. A family's label part composes it (ContextMenu's, track 7).
/// <summary>The behavior of a menu group label: its id's registration with the group and its attributes.</summary>
/// <param name="familyName">The label's family, which names the group in the context error.</param>
internal sealed class MenuGroupLabelBehavior(string familyName) : IDisposable
{
    private MenuGroupContext? group;
    private string? registeredId;

    /// <inheritdoc />
    public void Dispose() => group?.SetLabelId(null, registeredId);

    /// <summary>Requires the enclosing group.</summary>
    /// <param name="group">The cascaded group context, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The label is outside a group.</exception>
    public void Subscribe(MenuGroupContext? group) => this.group = group ?? throw MenuParts.MissingGroup(familyName);

    // MenuGroupLabel.tsx:25-30: an effect on the id, so a render of the group that changes nothing sets nothing, and two
    // mounted labels do not take the group's label from each other.
    /// <summary>Registers the label's id with the group when it changed.</summary>
    /// <param name="id">The label's id.</param>
    public void Apply(string? id)
    {
        if (string.Equals(registeredId, id, StringComparison.Ordinal))
        {
            return;
        }

        if (registeredId is not null)
        {
            group!.SetLabelId(null, registeredId);
        }

        registeredId = id;
        group!.SetLabelId(id);
    }

    /// <summary>Adds the label's attributes to a render.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes) => attributes.Set("aria-hidden", "true");
}
