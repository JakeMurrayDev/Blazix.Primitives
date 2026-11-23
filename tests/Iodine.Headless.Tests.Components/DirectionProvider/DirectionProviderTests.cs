using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// <see cref="DirectionProvider"/> at L2 (shared-utilities.md §4.15.1): Base UI's DirectionProvider.test.tsx titles through
/// an internal probe, because Base UI's public <c>useDirection</c> has no reader in the port (chosen by the user 2026-10-22, UTL
/// §4.15.1 port note), and the absent element (UTL-DOM-36).
/// </summary>
public sealed class DirectionProviderTests : IDisposable
{
    private readonly BunitContext context = new();

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "defaults useDirection to ltr outside a provider")]
    [Trait("Requirement", "UTL-I18N-01")]
    public void Defaults_useDirection_to_ltr_outside_a_provider()
    {
        IRenderedComponent<DirectionProbe> cut = context.Render<DirectionProbe>();

        Assert.Equal("ltr", cut.Find("[data-testid=direction]").TextContent);
    }

    [Fact(DisplayName = "provides the configured direction to descendants")]
    [Trait("Requirement", "UTL-I18N-01")]
    public void Provides_the_configured_direction_to_descendants()
    {
        IRenderedComponent<DirectionProvider> cut = context.Render<DirectionProvider>(p => p
            .Add(x => x.Direction, TextDirection.Rtl)
            .AddChildContent<IsolatedProbe>());

        Assert.Equal("rtl", cut.Find("[data-testid=direction]").TextContent);

        cut.Render(p => p.Add(x => x.Direction, TextDirection.Ltr));

        Assert.Equal("ltr", cut.Find("[data-testid=direction]").TextContent);
    }

    [Fact(DisplayName = "renders no element of its own and cascades a fixed context")]
    [Trait("Requirement", "UTL-DOM-36")]
    [Trait("Requirement", "UTL-I18N-03")]
    public void Renders_no_element_of_its_own()
    {
        IRenderedComponent<DirectionProvider> cut = context.Render<DirectionProvider>(p => p
            .Add(x => x.Direction, TextDirection.Rtl)
            .AddChildContent("<p>content</p>"));

        cut.MarkupMatches("<p>content</p>");
        Assert.True(cut.FindComponent<CascadingValue<DirectionProviderContext>>().Instance.IsFixed);
    }

    /// <summary>A probe whose parent never re-renders it, so only the provider's notification can update it.</summary>
    private sealed class IsolatedProbe : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<DirectionProbe>(0);
            builder.CloseComponent();
        }

        protected override bool ShouldRender() => false;
    }
}
