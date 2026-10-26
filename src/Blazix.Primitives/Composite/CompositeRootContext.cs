using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// CMP §6.3 and §6.6 (CompositeRootContext with CompositeListContext: the root provides the item registry), CMP-STR-01:
// every item registers with the nearest root. Cascaded fixed inside the root's element by the part (ADR 16 rule 9,
// BLZX2001); an item reaches the root through these methods, never through a re-cascade. CMP-STR-02 and CMP-EDGE-01: an
// item without a root is an error (an internal guard: no M2 public part reaches it; Radio registers only inside a group).
/// <summary>The registry a composite root shares with the items rendered inside it.</summary>
internal sealed class CompositeRootContext
{
    private readonly CompositeRootBehavior root;

    /// <summary>Initializes a new instance of the <see cref="CompositeRootContext"/> class.</summary>
    /// <param name="root">The root behavior that owns the registry.</param>
    internal CompositeRootContext(CompositeRootBehavior root)
    {
        this.root = root;
    }

    /// <summary>Gets the highlighted index the root last received from its browser-side behavior.</summary>
    /// <value>The index of the item that holds the group's tab stop; 0 until a report arrives.</value>
    public int HighlightedIndex => root.HighlightedIndex;

    /// <summary>Throws the error of a composite item rendered outside a composite root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <param name="part">The name of the item's part.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static CompositeRootContext Require(CompositeRootContext? context, string part)
    {
        return context ?? throw new InvalidOperationException(
            $"Blazix: `{part}` must be placed within a composite root. A composite item registers with the nearest root, which owns its tab stop.");
    }

    /// <summary>Adds an item to the root's registry.</summary>
    /// <param name="item">The item, whose registration is set.</param>
    public void Register(CompositeItemBehavior item) => root.Register(item);

    /// <summary>Removes an item from the root's registry and makes sure the root sends the change.</summary>
    /// <param name="item">The item.</param>
    public void Unregister(CompositeItemBehavior item) => root.Unregister(item);

    /// <summary>Records that a registered item changed its id, its consumer <c>tabindex</c> or its flags.</summary>
    public void RegistrationChanged() => root.RegistrationChanged();

    /// <summary>Records that a registered item rendered a new element under the same id.</summary>
    public void ElementReplaced() => root.ElementReplaced();

    /// <summary>Sends the root's options if they changed since the last send.</summary>
    /// <returns>A task that completes when the browser-side behavior has the options.</returns>
    public Task FlushAsync() => root.FlushAsync();

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<CompositeRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<CompositeRootContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<CompositeRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<CompositeRootContext>.ChildContent), content);
        builder.CloseComponent();
    };
}
