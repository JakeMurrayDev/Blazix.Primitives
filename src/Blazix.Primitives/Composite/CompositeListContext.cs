using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// CMP §6.6 (CompositeListContext, the registry a root-less CompositeList provides); M3 track 1, E1 (ADR 04 Amendments
// 2026-10-26, chosen by the user). A context of its own, separate from CompositeRootContext, as Base UI keeps the list
// context apart from the root's (CompositeRoot.tsx:84-86): a composite item never registers here and a list item never
// registers with a root, so a Toggle inside a TabsPanel does not infer composite mode (design point 6, option (a),
// pending the user). Cascaded fixed inside the list's element (ADR 16 rule 9, BLZX2001).
/// <summary>The registry a registration-only list shares with the items rendered inside it.</summary>
internal sealed class CompositeListContext
{
    private readonly CompositeListBehavior list;

    /// <summary>Initializes a new instance of the <see cref="CompositeListContext"/> class.</summary>
    /// <param name="list">The list behavior that owns the registry.</param>
    internal CompositeListContext(CompositeListBehavior list)
    {
        this.list = list;
    }

    /// <summary>Adds an item to the list's registry.</summary>
    /// <param name="item">The item, whose id is set.</param>
    public void Register(CompositeListItemBehavior item) => list.Register(item);

    /// <summary>Removes an item from the list's registry and makes sure the list sends the change.</summary>
    /// <param name="item">The item.</param>
    public void Unregister(CompositeListItemBehavior item) => list.Unregister(item);

    /// <summary>Records that a registered item changed its id.</summary>
    /// <param name="item">The item, whose id is already the new one.</param>
    /// <param name="previousId">The item's previous id.</param>
    public void RegistrationChanged(CompositeListItemBehavior item, string previousId) => list.RegistrationChanged(item, previousId);

    /// <summary>Records that a registered item rendered a new element under the same id.</summary>
    public void ElementReplaced() => list.ElementReplaced();

    /// <summary>Sends the list's items if they changed since the last send.</summary>
    /// <returns>A task that completes when the browser-side behavior has the items.</returns>
    public Task FlushAsync() => list.FlushAsync();

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<CompositeListContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<CompositeListContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<CompositeListContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<CompositeListContext>.ChildContent), content);
        builder.CloseComponent();
    };
}
