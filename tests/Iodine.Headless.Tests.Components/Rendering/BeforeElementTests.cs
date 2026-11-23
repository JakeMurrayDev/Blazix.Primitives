using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Consumers;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Rendering;

/// <summary>
/// Content before the host element (ADR 02 Amendments 2026-10-27, M4 track 1): the pipeline calls the part's
/// <c>BuildBeforeElement</c> before the host element on every render path, so a part can render siblings that precede its
/// element (a popup's leading focus guards); the base renders nothing there, and a part whose <c>Enabled</c> is
/// <see langword="false"/> renders nothing at all (RND-DOM-25).
/// </summary>
public sealed class BeforeElementTests : IDisposable
{
    private readonly BunitContext context = new();

    public BeforeElementTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact]
    public void The_content_precedes_the_default_element_as_its_sibling()
    {
        IRenderedComponent<GuardedPart> cut = context.Render<GuardedPart>(p => p.AddChildContent("inside"));

        cut.MarkupMatches("""<span tabindex="0" data-guard="before"></span><span>inside</span><i data-guard="after"></i>""");
    }

    [Fact]
    public void The_content_precedes_a_substitute_element()
    {
        IRenderedComponent<GuardedPart> cut = context.Render<GuardedPart>(p => p
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, "title", "t");
                builder.CloseElement();
            })));

        cut.MarkupMatches("""<span tabindex="0" data-guard="before"></span><button title="t"></button><i data-guard="after"></i>""");
    }

    [Fact]
    public void The_content_precedes_a_substitute_component()
    {
        IRenderedComponent<GuardedPart> cut = context.Render<GuardedPart>(p => p
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenComponent<CaptureTarget>(0);
                builder.AddComponentParameter(1, "title", "t");
                builder.CloseComponent();
            })));

        cut.MarkupMatches("""<span tabindex="0" data-guard="before"></span><span title="t"></span><i data-guard="after"></i>""");
    }

    [Fact]
    public void The_content_precedes_a_substitute_callback()
    {
        IRenderedComponent<GuardedPart> cut = context.Render<GuardedPart>(p => p
            .Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => builder =>
            {
                builder.OpenElement(0, "em");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.CloseElement();
            })));

        cut.MarkupMatches("""<span tabindex="0" data-guard="before"></span><em></em><i data-guard="after"></i>""");
    }

    [Fact]
    public void A_part_that_is_not_enabled_renders_neither_the_content_before_its_element_nor_the_element()
    {
        IRenderedComponent<GuardedPart> cut = context.Render<GuardedPart>(p => p.Add(x => x.Enabled, false));

        cut.MarkupMatches(string.Empty);
        Assert.Equal(0, cut.Instance.BeforeCalls);
    }

    [Fact]
    public void The_content_precedes_the_element_again_on_every_render()
    {
        IRenderedComponent<GuardedPart> cut = context.Render<GuardedPart>();

        cut.Render(p => p.AddChildContent("again"));

        Assert.Equal(2, cut.Instance.BeforeCalls);
        cut.MarkupMatches("""<span tabindex="0" data-guard="before"></span><span>again</span><i data-guard="after"></i>""");
    }

    [Fact]
    public void The_base_renders_nothing_before_the_element()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p.AddUnmatched("data-testid", "root"));

        AngleSharp.Dom.INode only = Assert.Single(cut.Nodes);
        Assert.Equal("root", ((AngleSharp.Dom.IElement)only).GetAttribute("data-testid"));
    }

    /// <summary>
    /// A part that renders a guard before its element and a marker after it, each numbered from 0 inside the pipeline's
    /// regions.
    /// </summary>
    public sealed class GuardedPart : PartBase<EmptyState>
    {
        public int BeforeCalls { get; private set; }

        /// <inheritdoc />
        protected override string DefaultTagName => "span";

        /// <inheritdoc />
        protected override EmptyState State => EmptyState.Instance;

        /// <inheritdoc />
        protected override void BuildBeforeElement(RenderTreeBuilder builder)
        {
            BeforeCalls++;
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "tabindex", "0");
            builder.AddAttribute(2, "data-guard", "before");
            builder.CloseElement();
        }

        /// <inheritdoc />
        protected override void BuildAfterElement(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "i");
            builder.AddAttribute(1, "data-guard", "after");
            builder.CloseElement();
        }
    }
}
