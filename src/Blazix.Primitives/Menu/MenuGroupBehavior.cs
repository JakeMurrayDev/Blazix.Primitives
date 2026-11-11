using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's Menu.Group (menu/group/MenuGroup.tsx; MNU-ARIA-10): role="group", aria-labelledby = the mounted label's id
// (MenuGroupContext, the PopoverIdSync form). A family's group part composes it (ContextMenu's, track 7).
/// <summary>The behavior of a menu group: the label's context it cascades and the group's attributes.</summary>
/// <param name="familyName">The group's family, which names the root in the context error.</param>
/// <param name="requestRender">Asks the part to render.</param>
internal sealed class MenuGroupBehavior(string familyName, Action requestRender) : IDisposable
{
    private readonly MenuGroupContext group = new();
    private IDisposable? subscription;

    /// <inheritdoc />
    public void Dispose() => subscription?.Dispose();

    /// <summary>Requires the enclosing menu and follows the label's id.</summary>
    /// <param name="root">The cascaded menu context, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The group is outside a menu.</exception>
    public void Subscribe(MenuRootContext? root)
    {
        MenuRootContext.Require(root, familyName);
        subscription = group.Subscribe(requestRender);
    }

    /// <summary>Returns the group's content inside the label's context.</summary>
    /// <param name="content">The part's content.</param>
    /// <returns>The content with the context cascaded.</returns>
    public RenderFragment? Cascade(RenderFragment? content) => group.Cascade(content);

    /// <summary>Adds the group's attributes to a render.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        attributes.Set("role", "group");
        if (group.LabelId is { } labelId)
        {
            attributes.Set("aria-labelledby", labelId);
        }
    }
}
