using System.Globalization;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>
/// A test-only item of a registration-only list for the <c>composite-*</c> fixtures, the counterpart of
/// <c>TestCompositeListItem</c> in the L2 project: a <c>div</c> composing <see cref="CompositeListItemBehavior"/> that
/// renders its index as <c>data-index</c>, as TabsPanel does (TAB-DOM-17).
/// </summary>
public sealed class FixtureCompositeListItem : PartBase<EmptyState>
{
    private CompositeListItemBehavior? behavior;

    [CascadingParameter]
    private CompositeListContext? ListContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        behavior = new CompositeListItemBehavior(ListContext, StateHasChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.SetRegistration(Id);
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
        attributes.Set("data-index", behavior!.Index.ToString(CultureInfo.InvariantCulture));
    }
}
