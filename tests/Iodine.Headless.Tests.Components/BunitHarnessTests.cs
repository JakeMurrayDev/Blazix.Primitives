using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>Proves the L2 runner works: a component renders through a <see cref="BunitContext"/>.</summary>
public sealed class BunitHarnessTests
{
    [Fact]
    public void A_div_renders_through_a_bunit_context()
    {
        using BunitContext context = new();

        IRenderedComponent<DivComponent> cut = context.Render<DivComponent>();

        cut.MarkupMatches("<div></div>");
    }

    private sealed class DivComponent : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.CloseElement();
        }
    }
}
