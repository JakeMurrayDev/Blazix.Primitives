using System.Globalization;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A test-only item of a registration-only list (M3 track 1, E1): a <c>div</c> that composes
/// <see cref="CompositeListItemBehavior"/> as TabsPanel and AccordionItem will, and renders the index it reads as
/// <c>data-index</c>. Outside a list it is inert.
/// </summary>
internal sealed class TestCompositeListItem : PartBase<EmptyState>
{
    private CompositeListItemBehavior? behavior;

    [CascadingParameter]
    private CompositeListContext? ListContext { get; set; }

    public int Index => behavior!.Index;

    public int Renders { get; private set; }

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
        Renders++;
        attributes.Set("data-index", behavior!.Index.ToString(CultureInfo.InvariantCulture));
    }
}
