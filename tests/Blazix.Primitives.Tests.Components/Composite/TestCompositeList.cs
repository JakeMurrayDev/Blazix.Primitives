using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A test-only registration-only list (M3 track 1, E1): a <c>div</c> that composes <see cref="CompositeListBehavior"/> as
/// TabsRoot (its panels) and AccordionRoot (its items) will. Base UI's <c>CompositeList</c> is internal (CMP §3).
/// </summary>
internal sealed class TestCompositeList : PartBase<EmptyState>
{
    private CompositeListBehavior? behavior;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    public CompositeListBehavior Behavior => behavior!;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new CompositeListBehavior(JSRuntime, IdGenerator, StateHasChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = behavior!.Context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        return behavior!.AfterRenderAsync(Enabled, Element, Id);
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
        behavior!.AddAttributes(attributes, Id);
    }
}
