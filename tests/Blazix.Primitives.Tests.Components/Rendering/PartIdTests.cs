using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Rendering;

/// <summary>Generated and supplied ids on a rendered part (RND-DOM-27..32, ADR 02 decision 5), with the [L2] titles of <c>useId.test.tsx</c> and <c>useId.react17.test.tsx</c>.</summary>
public sealed class PartIdTests : IDisposable
{
    private readonly BunitContext context = new();

    public PartIdTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "returns the provided ID")]
    [Trait("Requirement", "RND-DOM-28")]
    public void Returns_the_provided_ID()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p.AddUnmatched("id", "some-id"));
        Assert.Equal("some-id", Root(cut).Id);
        Assert.Equal("some-id", cut.Instance.ResolvedId);

        cut.Render(p => p.AddUnmatched("id", "another-id"));
        Assert.Equal("another-id", Root(cut).Id);
    }

    [Fact(DisplayName = "returns an empty string id override as-is")]
    [Trait("Requirement", "RND-DOM-28")]
    public void Returns_an_empty_string_id_override_as_is()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p.AddUnmatched("id", ""));

        Assert.True(Root(cut).HasAttribute("id"));
        Assert.Equal("", Root(cut).GetAttribute("id"));
        Assert.Equal("", cut.Instance.ResolvedId);
    }

    [Fact(DisplayName = "generates an ID if one isn't provided")]
    [Trait("Requirement", "RND-DOM-27")]
    public void Generates_an_ID_if_one_isnt_provided()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>();

        Assert.StartsWith("base-ui-", Root(cut).Id);
        Assert.Equal(Root(cut).Id, cut.Instance.ResolvedId);

        cut.Render(p => p.AddUnmatched("id", "another-id"));
        Assert.Equal("another-id", Root(cut).Id);
    }

    [Fact(DisplayName = "generates an id when the provided id is removed")]
    [Trait("Requirement", "RND-DOM-30")]
    public void Generates_an_id_when_the_provided_id_is_removed()
    {
        // Blazor keeps a parameter the parent stops supplying, so a consumer removes an attribute by rendering it
        // with a null value (`id="@maybeNull"`) or by rendering a bag without it; both forms are covered.
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p.AddUnmatched("id", "provided"));
        Assert.Equal("provided", Root(cut).Id);

        cut.Render(p => p.AddUnmatched("id", null!));
        Assert.StartsWith("base-ui-", Root(cut).Id);

        cut.Render(p => p.AddUnmatched("id", "provided"));
        Assert.Equal("provided", Root(cut).Id);

        cut.Render(p => p.AddUnmatched("data-other", "x"));
        Assert.StartsWith("base-ui-", Root(cut).Id);
    }

    [Fact(DisplayName = "keeps the generated id when the provided id is added and removed again")]
    [Trait("Requirement", "RND-DOM-30")]
    public void Keeps_the_generated_id_when_the_provided_id_is_added_and_removed_again()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>();
        string generated = Root(cut).Id!;

        cut.Render(p => p.AddUnmatched("id", "provided"));
        Assert.Equal("provided", Root(cut).Id);

        cut.Render(p => p.AddUnmatched("id", null!));
        Assert.Equal(generated, Root(cut).Id);
    }

    [Fact(DisplayName = "does not regenerate the id on subsequent renders")]
    [Trait("Requirement", "RND-DOM-29")]
    public void Does_not_regenerate_the_id_on_subsequent_renders()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>();
        string generated = Root(cut).Id!;

        cut.Render(p => p.Add(x => x.Label, "changed"));
        cut.InvokeAsync(() => cut.Instance.Refresh());

        Assert.Equal(generated, Root(cut).Id);
    }

    [Fact(DisplayName = "parts draw ids in render order whether or not an id was supplied")]
    [Trait("Requirement", "RND-DOM-31")]
    public void Parts_draw_ids_in_render_order_whether_or_not_an_id_was_supplied()
    {
        IRenderedComponent<IComponent> cut = context.Render(builder =>
        {
            builder.OpenComponent<SampleRoot>(0);
            builder.AddComponentParameter(1, "data-testid", "first");
            builder.CloseComponent();
            builder.OpenComponent<SampleRoot>(2);
            builder.AddComponentParameter(3, "data-testid", "second");
            builder.AddComponentParameter(4, "id", "supplied");
            builder.CloseComponent();
            builder.OpenComponent<SampleRoot>(5);
            builder.AddComponentParameter(6, "data-testid", "third");
            builder.CloseComponent();
        });

        Assert.Equal("base-ui-1", cut.Find("[data-testid=first]").Id);
        Assert.Equal("supplied", cut.Find("[data-testid=second]").Id);
        Assert.Equal("base-ui-3", cut.Find("[data-testid=third]").Id);
    }

    [Fact(DisplayName = "a substitute element's own id wins over the supplied and the generated id")]
    [Trait("Requirement", "RND-DOM-32")]
    public void A_substitute_elements_own_id_wins_over_the_supplied_and_the_generated_id()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .AddUnmatched("id", "supplied")
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "id", "substitute");
                builder.CloseElement();
            })));

        Assert.Equal("substitute", Root(cut).Id);
        Assert.Equal("supplied", cut.Instance.ResolvedId);
    }

    private static IElement Root(IRenderedComponent<SampleRoot> cut) => cut.Nodes.OfType<IElement>().Single();
}
