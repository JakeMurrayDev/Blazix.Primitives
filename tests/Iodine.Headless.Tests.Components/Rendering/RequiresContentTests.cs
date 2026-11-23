using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The pipeline's opt-in "renders only with content" gate (ADR 02 Amendments 2026-11-18, Toast DP11, settled by the
/// orchestrator 2026-11-18; TST-DOM-16, TST-DOM-19): a part that sets <c>RequiresContent</c> and has no
/// <c>ChildContent</c> renders nothing, for its default element and for a substitute element or markup root without
/// children of its own; a substitute with children renders them; a childless substitute renders the part's content; a
/// substitute callback is always rendered. A part without the flag is unchanged.
/// </summary>
public sealed class RequiresContentTests : IDisposable
{
    private readonly BunitContext context = new();

    public RequiresContentTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: a part that requires content renders nothing without content and without a substitute")]
    [Trait("Requirement", "TST-DOM-16")]
    public void A_part_that_requires_content_renders_nothing_without_content_and_without_a_substitute()
    {
        IRenderedComponent<SampleContentPart> cut = context.Render<SampleContentPart>(p => p.AddUnmatched("data-testid", "part"));

        Assert.Empty(cut.FindAll("[data-testid=part]"));
        Assert.Null(cut.Instance.Element);
    }

    [Fact(DisplayName = "port: a part that requires content renders a substitute element with children of its own")]
    [Trait("Requirement", "TST-DOM-16")]
    public void A_part_that_requires_content_renders_a_substitute_element_with_children_of_its_own()
    {
        IRenderedComponent<SampleContentPart> cut = context.Render<SampleContentPart>(p => p.AddUnmatched("Render", (RenderFragment)(builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "substitute");
            builder.AddContent(2, "render prop content");
            builder.CloseElement();
        })));

        Assert.Equal("render prop content", cut.Find("[data-testid=substitute]").TextContent);
        Assert.NotNull(cut.Instance.Element);
    }

    [Fact(DisplayName = "port: a part that requires content renders a substitute markup root with children of its own")]
    [Trait("Requirement", "TST-DOM-16")]
    public void A_part_that_requires_content_renders_a_substitute_markup_root_with_children_of_its_own()
    {
        IRenderedComponent<SampleContentPart> cut = context.Render<SampleContentPart>(p => p.AddUnmatched("Render", (RenderFragment)(builder =>
            builder.AddMarkupContent(0, "<div data-testid=\"substitute\">markup content</div>"))));

        Assert.Equal("markup content", cut.Find("[data-testid=substitute]").TextContent);
    }

    [Fact(DisplayName = "port: a part that requires content renders nothing for a childless substitute without content")]
    [Trait("Requirement", "TST-DOM-16")]
    public void A_part_that_requires_content_renders_nothing_for_a_childless_substitute_without_content()
    {
        IRenderedComponent<SampleContentPart> cut = context.Render<SampleContentPart>(p => p.AddUnmatched("Render", (RenderFragment)(builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "substitute");
            builder.CloseElement();
        })));

        Assert.Empty(cut.FindAll("[data-testid=substitute]"));
        Assert.Null(cut.Instance.Element);
    }

    [Fact(DisplayName = "port: a part that requires content renders nothing for a childless substitute markup root without content")]
    [Trait("Requirement", "TST-DOM-16")]
    public void A_part_that_requires_content_renders_nothing_for_a_childless_substitute_markup_root_without_content()
    {
        IRenderedComponent<SampleContentPart> cut = context.Render<SampleContentPart>(p => p.AddUnmatched("Render", (RenderFragment)(builder =>
            builder.AddMarkupContent(0, "<div data-testid=\"substitute\"></div>"))));

        Assert.Empty(cut.FindAll("[data-testid=substitute]"));
    }

    [Fact(DisplayName = "port: a part that requires content renders its content through a childless substitute")]
    [Trait("Requirement", "TST-DOM-16")]
    public void A_part_that_requires_content_renders_its_content_through_a_childless_substitute()
    {
        IRenderedComponent<SampleContentPart> cut = context.Render<SampleContentPart>(p => p
            .AddChildContent("part content")
            .AddUnmatched("Render", (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "data-testid", "substitute");
                builder.CloseElement();
            })));

        Assert.Equal("part content", cut.Find("[data-testid=substitute]").TextContent);
    }

    [Fact(DisplayName = "port: a part that requires content always renders a substitute callback")]
    [Trait("Requirement", "TST-DOM-16")]
    public void A_part_that_requires_content_always_renders_a_substitute_callback()
    {
        IRenderedComponent<SampleContentPart> cut = context.Render<SampleContentPart>(p => p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.AddAttribute(2, "data-testid", "callback");
            builder.AddContent(3, "render fn");
            builder.CloseElement();
        })));

        Assert.Equal("render fn", cut.Find("[data-testid=callback]").TextContent);
    }

    [Fact(DisplayName = "port: a part that does not require content renders its element without content")]
    [Trait("Requirement", "RND-DOM-25")]
    public void A_part_that_does_not_require_content_renders_its_element_without_content()
    {
        IRenderedComponent<SampleContentPart> cut = context.Render<SampleContentPart>(p => p
            .Add(x => x.Requires, false)
            .AddUnmatched("data-testid", "part"));

        Assert.NotNull(cut.Find("[data-testid=part]"));
    }
}

/// <summary>A part that opts into the content gate unless <see cref="Requires"/> is <see langword="false"/>.</summary>
public sealed class SampleContentPart : PartBase<EmptyState>
{
    /// <summary>Gets or sets a value indicating whether the part requires content.</summary>
    [Parameter]
    public bool Requires { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool RequiresContent => Requires;
}
