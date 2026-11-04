using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// The list a family's Root cascades with `IsFixed` (ADR 16 rule 9, BLZX2001): its handles (composite roots in list mode,
// M5 track 1) build their options from it and route their reports into it (ADR 04 Amendments 2026-11-03 (1)).
/// <summary>The cascaded list of one popup: the options its handles send and the receiver of their reports.</summary>
internal sealed class ListNavigationContext
{
    private readonly ListNavigationBehavior list;

    /// <summary>Initializes a new instance of the <see cref="ListNavigationContext"/> class.</summary>
    /// <param name="list">The list.</param>
    internal ListNavigationContext(ListNavigationBehavior list)
    {
        this.list = list;
    }

    /// <summary>Gets the list.</summary>
    /// <value>The list behavior of the family's Root.</value>
    public ListNavigationBehavior List => list;

    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<ListNavigationContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ListNavigationContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<ListNavigationContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ListNavigationContext>.ChildContent), content);
        builder.CloseComponent();
    };
}
