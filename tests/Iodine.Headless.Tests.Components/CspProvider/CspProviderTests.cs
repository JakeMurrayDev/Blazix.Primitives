using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// <see cref="CspProvider"/> at L2 (shared-utilities.md §4.12): the absent element and its fixed cascade (UTL-DOM-36), the
/// default configuration and the nearest provider (UTL-DOM-35), read by a test-only reader as the style emitter reads them.
/// </summary>
public sealed class CspProviderTests : IDisposable
{
    private readonly BunitContext context = new();

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: CspProvider renders no element of its own and cascades a fixed context")]
    [Trait("Requirement", "UTL-DOM-36")]
    public void Renders_no_element_of_its_own_and_cascades_a_fixed_context()
    {
        IRenderedComponent<CspProvider> cut = context.Render<CspProvider>(p => p
            .Add(x => x.Nonce, "n")
            .AddChildContent("<p>content</p>"));

        cut.MarkupMatches("<p>content</p>");
        Assert.True(cut.FindComponent<CascadingValue<CspProviderContext>>().Instance.IsFixed);
    }

    [Fact(DisplayName = "port: without a provider the nonce is null and style elements render, and the nearest provider's values apply to every descendant")]
    [Trait("Requirement", "UTL-DOM-35")]
    public void The_default_configuration_and_the_nearest_provider()
    {
        IRenderedComponent<CspReader> outside = context.Render<CspReader>();
        Assert.Equal("nonce= disable=False", outside.Find("[data-testid=csp]").TextContent);

        IRenderedComponent<CspProvider> cut = context.Render<CspProvider>(p => p
            .Add(x => x.Nonce, "outer")
            .Add(x => x.DisableStyleElements, true)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<CspReader>(0);
                builder.CloseComponent();
                builder.OpenComponent<CspProvider>(1);
                builder.AddComponentParameter(2, nameof(CspProvider.Nonce), "inner");
                builder.AddComponentParameter(3, nameof(CspProvider.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<CspReader>(0);
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            }));

        Assert.Equal(["nonce=outer disable=True", "nonce=inner disable=False"], cut.FindAll("[data-testid=csp]").Select(element => element.TextContent));
    }

    [Fact(DisplayName = "port: a provider's changed values reach readers its parent does not re-render")]
    [Trait("Requirement", "UTL-DOM-35")]
    public void A_providers_changed_values_reach_readers_its_parent_does_not_re_render()
    {
        IRenderedComponent<CspProvider> cut = context.Render<CspProvider>(p => p
            .Add(x => x.Nonce, "a")
            .AddChildContent<IsolatedReader>());

        cut.Render(p => p.Add(x => x.Nonce, "b").Add(x => x.DisableStyleElements, true));

        Assert.Equal("nonce=b disable=True", cut.Find("[data-testid=csp]").TextContent);
    }

    /// <summary>A reader of the cascaded configuration, as the style emitter's caller reads it.</summary>
    private sealed class CspReader : ComponentBase, IDisposable
    {
        [CascadingParameter]
        private CspProviderContext? Csp { get; set; }

        public void Dispose() => Csp?.RemoveListener(StateHasChanged);

        protected override void OnInitialized() => Csp?.AddListener(StateHasChanged);

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-testid", "csp");
            builder.AddContent(2, $"nonce={CspProviderContext.ReadNonce(Csp)} disable={CspProviderContext.ReadDisableStyleElements(Csp)}");
            builder.CloseElement();
        }
    }

    /// <summary>A reader whose parent never re-renders it, so only the provider's notification can update it.</summary>
    private sealed class IsolatedReader : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CspReader>(0);
            builder.CloseComponent();
        }

        protected override bool ShouldRender() => false;
    }
}
