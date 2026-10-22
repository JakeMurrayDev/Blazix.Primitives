using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>
/// A test-only composite item for the <c>composite-*</c> fixtures, the counterpart of <c>TestCompositeItem</c> in the L2
/// project: an element of <see cref="Tag"/> composing <see cref="CompositeItemBehavior"/>.
/// </summary>
public sealed class FixtureCompositeItem : PartBase<EmptyState>
{
    private CompositeItemBehavior? behavior;

    [CascadingParameter]
    private CompositeRootContext? RootContext { get; set; }

    [Parameter]
    public string Tag { get; set; } = "div";

    /// <inheritdoc />
    protected override string DefaultTagName => Tag;

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        behavior = new CompositeItemBehavior(RootContext, nameof(FixtureCompositeItem));
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.SetRegistration(Id, AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        return behavior!.AfterRenderAsync(Element);
    }

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
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes);
    }
}
