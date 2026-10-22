using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Consumers;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Rendering;

/// <summary>
/// Content after the host element (ADR 02 Amendments 2026-10-22 item 2, M2 track 4): the pipeline calls the part's
/// <c>BuildAfterElement</c> after the host element on every render path, so a part can render siblings of its element
/// (the hidden inputs of SWT-STR-01, CHK-STR-01, RAD-STR-01); the base renders nothing there, and a part whose
/// <c>Enabled</c> is <see langword="false"/> renders nothing at all (RND-DOM-25).
/// </summary>
public sealed class AfterElementTests : IDisposable
{
    private readonly BunitContext context = new();

    public AfterElementTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact]
    public void The_content_follows_the_default_element_as_its_sibling()
    {
        IRenderedComponent<SiblingPart> cut = context.Render<SiblingPart>(p => p.AddChildContent("inside"));

        cut.MarkupMatches("""<span>inside</span><input type="checkbox" aria-hidden="true">""");
    }

    [Fact]
    public void The_content_follows_a_substitute_element()
    {
        IRenderedComponent<SiblingPart> cut = context.Render<SiblingPart>(p => p
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, "title", "t");
                builder.CloseElement();
            })));

        cut.MarkupMatches("""<button title="t"></button><input type="checkbox" aria-hidden="true">""");
    }

    [Fact]
    public void The_content_follows_a_substitute_component()
    {
        IRenderedComponent<SiblingPart> cut = context.Render<SiblingPart>(p => p
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenComponent<CaptureTarget>(0);
                builder.AddComponentParameter(1, "title", "t");
                builder.CloseComponent();
            })));

        cut.MarkupMatches("""<span title="t"></span><input type="checkbox" aria-hidden="true">""");
    }

    [Fact]
    public void The_content_follows_a_substitute_callback()
    {
        IRenderedComponent<SiblingPart> cut = context.Render<SiblingPart>(p => p
            .Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => builder =>
            {
                builder.OpenElement(0, "em");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.CloseElement();
            })));

        cut.MarkupMatches("""<em></em><input type="checkbox" aria-hidden="true">""");
    }

    [Fact]
    public void A_part_that_is_not_enabled_renders_neither_its_element_nor_the_content_after_it()
    {
        IRenderedComponent<SiblingPart> cut = context.Render<SiblingPart>(p => p.Add(x => x.Enabled, false));

        cut.MarkupMatches(string.Empty);
        Assert.Equal(0, cut.Instance.Calls);
    }

    [Fact]
    public void The_content_follows_the_element_again_on_every_render()
    {
        IRenderedComponent<SiblingPart> cut = context.Render<SiblingPart>();

        cut.Render(p => p.AddChildContent("again"));

        Assert.Equal(2, cut.Instance.Calls);
        cut.MarkupMatches("""<span>again</span><input type="checkbox" aria-hidden="true">""");
    }

    [Fact]
    public void The_base_renders_nothing_after_the_element()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p.AddUnmatched("data-testid", "root"));

        Assert.Single(cut.Nodes);
    }

    /// <summary>A part that renders a sibling input after its element, numbered from 0 inside the pipeline's region.</summary>
    public sealed class SiblingPart : PartBase<EmptyState>
    {
        public int Calls { get; private set; }

        /// <inheritdoc />
        protected override string DefaultTagName => "span";

        /// <inheritdoc />
        protected override EmptyState State => EmptyState.Instance;

        /// <inheritdoc />
        protected override void BuildAfterElement(RenderTreeBuilder builder)
        {
            Calls++;
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "type", "checkbox");
            builder.AddAttribute(2, "aria-hidden", "true");
            builder.CloseElement();
        }
    }
}
