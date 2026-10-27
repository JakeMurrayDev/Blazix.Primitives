using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Consumers;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Rendering;

/// <summary>
/// The opt-in host wrapper (ADR 02 Amendments 2026-10-27, settled by the orchestrator 2026-10-27; M4 track 1): a part that
/// opts in gets one wrapper element around its host element's frames on every render path, with its own attributes; the
/// content before and after the element renders outside the wrapper, <c>Element</c> captures the host, and
/// <c>FinishAttributes</c> concerns the host alone. The portal opts in, so that its node is the wrapper's only child while
/// its focus guards and owner are the wrapper's siblings (ADR 04 Amendments 2026-10-27, decision 5).
/// </summary>
public sealed class HostWrapperTests : IDisposable
{
    private readonly BunitContext context = new();

    public HostWrapperTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact]
    public void The_wrapper_surrounds_the_default_element_and_the_content_before_and_after_it_sits_outside()
    {
        IRenderedComponent<WrappedPart> cut = context.Render<WrappedPart>(p => p.AddChildContent("inside"));

        cut.MarkupMatches("""
            <span data-guard="before"></span>
            <div style="display: contents" data-wrapper=""><span data-host="" data-finished="">inside</span></div>
            <span data-guard="after"></span>
            """);
    }

    [Fact]
    public void The_wrapper_surrounds_a_substitute_element()
    {
        IRenderedComponent<WrappedPart> cut = context.Render<WrappedPart>(p => p
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "section");
                builder.AddAttribute(1, "title", "t");
                builder.CloseElement();
            })));

        cut.MarkupMatches("""
            <span data-guard="before"></span>
            <div style="display: contents" data-wrapper=""><section title="t" data-host="" data-finished=""></section></div>
            <span data-guard="after"></span>
            """);
    }

    [Fact]
    public void The_wrapper_surrounds_a_substitute_component()
    {
        IRenderedComponent<WrappedPart> cut = context.Render<WrappedPart>(p => p
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenComponent<CaptureTarget>(0);
                builder.AddComponentParameter(1, "title", "t");
                builder.CloseComponent();
            })));

        cut.MarkupMatches("""
            <span data-guard="before"></span>
            <div style="display: contents" data-wrapper=""><span title="t" data-host="" data-finished=""></span></div>
            <span data-guard="after"></span>
            """);
    }

    [Fact]
    public void The_wrapper_surrounds_the_output_of_a_substitute_callback()
    {
        IRenderedComponent<WrappedPart> cut = context.Render<WrappedPart>(p => p
            .Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => builder =>
            {
                builder.OpenElement(0, "em");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.CloseElement();
            })));

        cut.MarkupMatches("""
            <span data-guard="before"></span>
            <div style="display: contents" data-wrapper=""><em data-host="" data-finished=""></em></div>
            <span data-guard="after"></span>
            """);
    }

    [Fact]
    public void The_element_captures_the_host_inside_the_wrapper()
    {
        IRenderedComponent<WrappedPart> cut = context.Render<WrappedPart>();

        IElement host = cut.Find("[data-host]");
        IElement wrapper = cut.Find("[data-wrapper]");
        Assert.NotNull(cut.Instance.Element);
        Assert.Equal(cut.Instance.Element.Value.Id, host.GetAttribute("blazor:elementreference"));
        Assert.True(host.ParentElement!.HasAttribute("data-wrapper"));
        Assert.Null(wrapper.GetAttribute("blazor:elementreference"));
    }

    [Fact]
    public void The_wrapper_keeps_its_attributes_on_every_render_and_the_host_stays_its_only_child()
    {
        IRenderedComponent<WrappedPart> cut = context.Render<WrappedPart>(p => p.AddChildContent("first"));

        cut.Render(p => p.AddChildContent("second").AddUnmatched("data-extra", "x"));

        Assert.Equal(2, cut.Instance.WrapperCalls);
        IElement wrapper = cut.Find("[data-wrapper]");
        Assert.Equal("display: contents", wrapper.GetAttribute("style"));
        Assert.Null(wrapper.GetAttribute("data-extra"));
        IElement only = Assert.Single(wrapper.Children);
        Assert.Equal("x", only.GetAttribute("data-extra"));
        Assert.Equal("second", only.TextContent);
    }

    [Fact]
    public void A_part_that_is_not_enabled_renders_neither_the_wrapper_nor_its_element()
    {
        IRenderedComponent<WrappedPart> cut = context.Render<WrappedPart>(p => p.AddChildContent("inside"));

        cut.Render(p => p.Add(x => x.Enabled, false));

        cut.MarkupMatches(string.Empty);
        Assert.Equal(1, cut.Instance.WrapperCalls);
    }

    /// <summary>
    /// A part that opts into the host wrapper, renders a guard before and after its element, and marks its host from
    /// <c>FinishAttributes</c>.
    /// </summary>
    public sealed class WrappedPart : PartBase<EmptyState>
    {
        public int WrapperCalls { get; private set; }

        /// <inheritdoc />
        protected override string DefaultTagName => "span";

        /// <inheritdoc />
        protected override EmptyState State => EmptyState.Instance;

        /// <inheritdoc />
        protected override string? HostWrapperTagName => "div";

        /// <inheritdoc />
        protected override void AddHostWrapperAttributes(AttributeList attributes)
        {
            WrapperCalls++;
            attributes.Set("style", "display: contents");
            attributes.Set("data-wrapper", string.Empty);
        }

        /// <inheritdoc />
        protected override void AddPartAttributes(AttributeList attributes) => attributes.Set("data-host", string.Empty);

        /// <inheritdoc />
        protected override void FinishAttributes(AttributeList attributes) => attributes.Set("data-finished", string.Empty);

        /// <inheritdoc />
        protected override void BuildBeforeElement(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-guard", "before");
            builder.CloseElement();
        }

        /// <inheritdoc />
        protected override void BuildAfterElement(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-guard", "after");
            builder.CloseElement();
        }
    }
}
