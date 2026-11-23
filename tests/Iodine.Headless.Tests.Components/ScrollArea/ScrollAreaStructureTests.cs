using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// What the six parts render before the first measurement, and the structural errors (SCR §2, §5; SCR-STA-08, SCR-EDGE-13):
/// C# alone, the <c>scroll-area</c> module's imports answered by bUnit's loose interop mock.
/// </summary>
public sealed class ScrollAreaStructureTests : IDisposable
{
    private readonly BunitContext context = new();

    public ScrollAreaStructureTests()
    {
        context.Services.AddBlazixPrimitives();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "throws a descriptive error when rendered outside <ScrollArea.Root>")]
    [Trait("Requirement", "SCR-STR-02")]
    [Trait("Requirement", "SCR-EDGE-01")]
    public void Throws_a_descriptive_error_when_the_viewport_is_rendered_outside_the_root()
    {
        // Base UI's wording with the product prefix and code in backticks (plan §Settled, a dated deviation on the row).
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => context.Render<ScrollAreaViewport>());

        Assert.Equal("Blazix: `ScrollAreaRootContext` is missing. ScrollArea parts must be placed within `<ScrollAreaRoot>`.", error.Message);
    }

    [Fact(DisplayName = "port: the scrollbar, the corner and the thumb throw the root's error outside a root")]
    [Trait("Requirement", "SCR-STR-04")]
    [Trait("Requirement", "SCR-STR-06")]
    [Trait("Requirement", "SCR-EDGE-01")]
    public void The_scrollbar_the_corner_and_the_thumb_throw_the_roots_error_outside_a_root()
    {
        const string Message = "Blazix: `ScrollAreaRootContext` is missing. ScrollArea parts must be placed within `<ScrollAreaRoot>`.";

        Assert.Equal(Message, Assert.Throws<InvalidOperationException>(() => context.Render<ScrollAreaScrollbar>()).Message);
        Assert.Equal(Message, Assert.Throws<InvalidOperationException>(() => context.Render<ScrollAreaCorner>()).Message);
        Assert.Equal(Message, Assert.Throws<InvalidOperationException>(() => context.Render<ScrollAreaThumb>()).Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <ScrollArea.Viewport>")]
    [Trait("Requirement", "SCR-STR-03")]
    [Trait("Requirement", "SCR-EDGE-02")]
    public void Throws_a_descriptive_error_when_the_content_is_rendered_outside_the_viewport()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => context.Render<ScrollAreaRoot>(p => p
            .AddChildContent<ScrollAreaContent>()));

        Assert.Equal("Blazix: `ScrollAreaViewportContext` missing. ScrollAreaViewport parts must be placed within `<ScrollAreaViewport>`.", error.Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <ScrollArea.Scrollbar>")]
    [Trait("Requirement", "SCR-STR-05")]
    [Trait("Requirement", "SCR-EDGE-03")]
    public void Throws_a_descriptive_error_when_the_thumb_is_rendered_outside_the_scrollbar()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => context.Render<ScrollAreaRoot>(p => p
            .AddChildContent<ScrollAreaThumb>()));

        Assert.Equal("Blazix: `ScrollAreaScrollbarContext` is missing. ScrollAreaScrollbar parts must be placed within `<ScrollAreaScrollbar>`.", error.Message);
    }

    [Fact(DisplayName = "port: the root renders role presentation, position relative and the corner variables at 0px after the scrollbar style")]
    [Trait("Requirement", "SCR-ARIA-01")]
    [Trait("Requirement", "SCR-DOM-01")]
    [Trait("Requirement", "SCR-DOM-02")]
    [Trait("Requirement", "SCR-DOM-04")]
    [Trait("Requirement", "SCR-DOM-05")]
    [Trait("Requirement", "SCR-EDGE-13")]
    public void The_root_renders_its_role_its_style_and_the_scrollbar_style_before_it()
    {
        IRenderedComponent<ScrollAreaRoot> cut = context.Render<ScrollAreaRoot>(p => p.AddUnmatched("data-testid", "root"));

        IElement root = cut.Find("[data-testid=root]");
        Assert.Equal("presentation", root.GetAttribute("role"));
        Assert.Equal("position: relative; --scroll-area-corner-height: 0px; --scroll-area-corner-width: 0px", root.GetAttribute("style"));
        Assert.Null(root.GetAttribute("id"));
        Assert.DoesNotContain(root.Attributes, a => a.Name.StartsWith("data-", StringComparison.Ordinal) && a.Name != "data-testid");
        IElement style = Assert.IsAssignableFrom<IElement>(root.PreviousElementSibling);
        Assert.Equal("style", style.LocalName);
        Assert.Equal(".base-ui-disable-scrollbar{scrollbar-width:none}.base-ui-disable-scrollbar::-webkit-scrollbar{display:none}", style.TextContent);
    }

    [Fact(DisplayName = "port: a consumer role and position replace the root's")]
    [Trait("Requirement", "SCR-ARIA-01")]
    [Trait("Requirement", "SCR-DOM-01")]
    public void A_consumer_role_and_position_replace_the_roots()
    {
        IRenderedComponent<ScrollAreaRoot> cut = context.Render<ScrollAreaRoot>(p => p
            .AddUnmatched("role", "region")
            .AddUnmatched("style", "position: absolute"));

        IElement root = cut.Find("div");
        Assert.Equal("region", root.GetAttribute("role"));
        Assert.Equal("position: absolute; --scroll-area-corner-height: 0px; --scroll-area-corner-width: 0px;", root.GetAttribute("style"));
    }

    [Fact(DisplayName = "port: the root's state holds the eight fields of the pre-measurement state")]
    [Trait("Requirement", "SCR-STA-01")]
    [Trait("Requirement", "SCR-STA-08")]
    public void The_roots_state_holds_the_pre_measurement_state()
    {
        ScrollAreaRootState? seen = null;

        context.Render<ScrollAreaRoot>(p => p.Add(x => x.ClassValue, s =>
        {
            seen = s;
            return null;
        }));

        Assert.Equal(new ScrollAreaRootState(false, false, false, false, false, false, false, true), seen);
    }

    [Fact(DisplayName = "does not use a presentational role on the focusable viewport")]
    [Trait("Requirement", "SCR-ARIA-03")]
    [Trait("Requirement", "SCR-API-05")]
    public void Does_not_use_a_presentational_role_on_the_focusable_viewport()
    {
        IRenderedComponent<ScrollAreaRoot> cut = context.Render<ScrollAreaRoot>(p => p
            .AddChildContent<ScrollAreaViewport>(v => v.AddUnmatched("tabindex", 0).AddUnmatched("data-testid", "viewport")));

        IElement viewport = cut.Find("[data-testid=viewport]");
        Assert.Equal("0", viewport.GetAttribute("tabindex"));
        Assert.False(viewport.HasAttribute("role"));
    }

    [Fact(DisplayName = "port: the viewport renders the scrollbar-hiding class, overflow scroll, tabindex -1 and its data-id before the first measurement")]
    [Trait("Requirement", "SCR-DOM-06")]
    [Trait("Requirement", "SCR-DOM-07")]
    [Trait("Requirement", "SCR-DOM-08")]
    [Trait("Requirement", "SCR-DOM-09")]
    [Trait("Requirement", "SCR-EDGE-13")]
    public void The_viewport_renders_its_class_style_tabindex_and_data_id()
    {
        IRenderedComponent<ScrollAreaRoot> cut = context.Render<ScrollAreaRoot>(p => p
            .AddChildContent<ScrollAreaViewport>(v => v.AddUnmatched("class", "consumer").AddUnmatched("style", "height: 10px")));

        IElement viewport = cut.Find("[data-id]");
        Assert.Equal("consumer base-ui-disable-scrollbar", viewport.GetAttribute("class"));
        Assert.Equal("overflow: scroll; height: 10px;", viewport.GetAttribute("style"));
        Assert.Equal("-1", viewport.GetAttribute("tabindex"));
        Assert.Matches("^base-ui-[0-9]+-viewport$", viewport.GetAttribute("data-id"));
        Assert.DoesNotContain(viewport.Attributes, a => a.Name.StartsWith("data-", StringComparison.Ordinal) && a.Name != "data-id");
    }

    [Fact(DisplayName = "port: the content renders role presentation and min-width fit-content under the consumer's style")]
    [Trait("Requirement", "SCR-ARIA-02")]
    [Trait("Requirement", "SCR-DOM-13")]
    public void The_content_renders_its_role_and_style()
    {
        IRenderedComponent<ScrollAreaRoot> cut = context.Render<ScrollAreaRoot>(p => p
            .AddChildContent<ScrollAreaViewport>(v => v
                .AddChildContent<ScrollAreaContent>(c => c.AddUnmatched("data-testid", "content").AddUnmatched("style", "padding: 4px"))));

        IElement content = cut.Find("[data-testid=content]");
        Assert.Equal("presentation", content.GetAttribute("role"));
        Assert.Equal("min-width: fit-content; padding: 4px;", content.GetAttribute("style"));
    }

    [Fact(DisplayName = "is hidden from the accessibility tree by default")]
    [Trait("Requirement", "SCR-ARIA-04")]
    public void The_scrollbar_is_hidden_from_the_accessibility_tree_by_default()
    {
        IRenderedComponent<ScrollAreaRoot> cut = RenderScrollbar(s => s.Add(x => x.KeepMounted, true));

        Assert.Equal("true", cut.Find("[data-testid=scrollbar]").GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "allows overriding aria-hidden")]
    [Trait("Requirement", "SCR-ARIA-04")]
    public void The_scrollbar_allows_overriding_aria_hidden()
    {
        // Base UI removes it with `aria-hidden={undefined}`; a null value is not expressible in Razor (ADR 02), so the port's
        // override is a value (the MenuGroupLabel precedent).
        IRenderedComponent<ScrollAreaRoot> cut = RenderScrollbar(s => s.Add(x => x.KeepMounted, true).AddUnmatched("aria-hidden", "false"));

        Assert.Equal("false", cut.Find("[data-testid=scrollbar]").GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "sets the orientation data attribute")]
    [Trait("Requirement", "SCR-API-06")]
    [Trait("Requirement", "SCR-DOM-16")]
    public void Sets_the_orientation_data_attribute()
    {
        IRenderedComponent<ScrollAreaRoot> cut = RenderScrollbar(s => s.Add(x => x.KeepMounted, true).Add(x => x.Orientation, Primitives.Orientation.Horizontal));

        Assert.Equal("horizontal", cut.Find("[data-testid=scrollbar]").GetAttribute("data-orientation"));
    }

    [Fact(DisplayName = "port: a scrollbar that is not kept mounted renders no element before the first measurement, and a kept-mounted one does")]
    [Trait("Requirement", "SCR-DOM-15")]
    [Trait("Requirement", "SCR-API-07")]
    [Trait("Requirement", "SCR-EDGE-13")]
    public void A_scrollbar_renders_only_when_kept_mounted_before_the_first_measurement()
    {
        IRenderedComponent<ScrollAreaRoot> cut = context.Render<ScrollAreaRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<ScrollAreaScrollbar>(0);
            builder.AddComponentParameter(1, nameof(ScrollAreaScrollbar.KeepMounted), true);
            builder.AddAttribute(2, "data-testid", "kept");
            builder.CloseComponent();
            builder.OpenComponent<ScrollAreaScrollbar>(3);
            builder.AddComponentParameter(4, nameof(ScrollAreaScrollbar.Orientation), Primitives.Orientation.Horizontal);
            builder.AddAttribute(5, "data-testid", "conditional");
            builder.CloseComponent();
        }));

        Assert.Single(cut.FindAll("[data-testid=kept]"));
        Assert.Empty(cut.FindAll("[data-testid=conditional]"));
        Assert.Null(cut.FindComponents<ScrollAreaScrollbar>()[1].Instance.Element);
    }

    [Fact(DisplayName = "port: the scrollbar renders its position, touch-action, user-select and insets, the thumb-size variable at 0px and its data-id")]
    [Trait("Requirement", "SCR-DOM-17")]
    [Trait("Requirement", "SCR-DOM-18")]
    [Trait("Requirement", "SCR-DOM-19")]
    [Trait("Requirement", "SCR-DOM-20")]
    [Trait("Requirement", "SCR-DOM-21")]
    [Trait("Requirement", "SCR-EDGE-13")]
    public void The_scrollbar_renders_its_styles_and_data_id()
    {
        IRenderedComponent<ScrollAreaRoot> cut = context.Render<ScrollAreaRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<ScrollAreaScrollbar>(0);
            builder.AddComponentParameter(1, nameof(ScrollAreaScrollbar.KeepMounted), true);
            builder.AddAttribute(2, "data-testid", "vertical");
            builder.CloseComponent();
            builder.OpenComponent<ScrollAreaScrollbar>(3);
            builder.AddComponentParameter(4, nameof(ScrollAreaScrollbar.Orientation), Primitives.Orientation.Horizontal);
            builder.AddComponentParameter(5, nameof(ScrollAreaScrollbar.KeepMounted), true);
            builder.AddAttribute(6, "data-testid", "horizontal");
            builder.CloseComponent();
        }));

        IElement vertical = cut.Find("[data-testid=vertical]");
        IElement horizontal = cut.Find("[data-testid=horizontal]");
        Assert.Equal(
            "position: absolute; touch-action: none; -webkit-user-select: none; user-select: none; top: 0; bottom: var(--scroll-area-corner-height); inset-inline-end: 0; --scroll-area-thumb-height: 0px",
            vertical.GetAttribute("style"));
        Assert.Equal(
            "position: absolute; touch-action: none; -webkit-user-select: none; user-select: none; inset-inline-start: 0; inset-inline-end: var(--scroll-area-corner-width); bottom: 0; --scroll-area-thumb-width: 0px",
            horizontal.GetAttribute("style"));
        Assert.Matches("^base-ui-[0-9]+-scrollbar$", vertical.GetAttribute("data-id"));
        Assert.Equal(vertical.GetAttribute("data-id"), horizontal.GetAttribute("data-id"));
        Assert.Equal("vertical", vertical.GetAttribute("data-orientation"));
    }

    [Fact(DisplayName = "port: the thumb is hidden and sized from its scrollbar's variable before the first measurement")]
    [Trait("Requirement", "SCR-DOM-22")]
    [Trait("Requirement", "SCR-DOM-23")]
    [Trait("Requirement", "SCR-EDGE-13")]
    public void The_thumb_is_hidden_and_sized_from_its_scrollbars_variable()
    {
        IRenderedComponent<ScrollAreaRoot> cut = context.Render<ScrollAreaRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<ScrollAreaScrollbar>(0);
            builder.AddComponentParameter(1, nameof(ScrollAreaScrollbar.KeepMounted), true);
            builder.AddComponentParameter(2, nameof(ScrollAreaScrollbar.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<ScrollAreaThumb>(0);
                b.AddAttribute(1, "data-testid", "thumb-y");
                b.CloseComponent();
            }));
            builder.CloseComponent();
            builder.OpenComponent<ScrollAreaScrollbar>(3);
            builder.AddComponentParameter(4, nameof(ScrollAreaScrollbar.Orientation), Primitives.Orientation.Horizontal);
            builder.AddComponentParameter(5, nameof(ScrollAreaScrollbar.KeepMounted), true);
            builder.AddComponentParameter(6, nameof(ScrollAreaScrollbar.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<ScrollAreaThumb>(0);
                b.AddAttribute(1, "data-testid", "thumb-x");
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }));

        IElement thumbY = cut.Find("[data-testid=thumb-y]");
        IElement thumbX = cut.Find("[data-testid=thumb-x]");
        Assert.Equal("visibility: hidden; height: var(--scroll-area-thumb-height)", thumbY.GetAttribute("style"));
        Assert.Equal("visibility: hidden; width: var(--scroll-area-thumb-width)", thumbX.GetAttribute("style"));
        Assert.Equal("vertical", thumbY.GetAttribute("data-orientation"));
        Assert.Equal("horizontal", thumbX.GetAttribute("data-orientation"));
        Assert.DoesNotContain(thumbY.Attributes, a => a.Name.StartsWith("data-", StringComparison.Ordinal) && a.Name is not ("data-testid" or "data-orientation"));
    }

    [Fact(DisplayName = "port: the thumb's state holds its axis's scrolling flag and the orientation")]
    [Trait("Requirement", "SCR-STA-04")]
    public void The_thumbs_state_holds_the_scrolling_flag_and_the_orientation()
    {
        ScrollAreaThumbState? seen = null;

        context.Render<ScrollAreaRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<ScrollAreaScrollbar>(0);
            builder.AddComponentParameter(1, nameof(ScrollAreaScrollbar.Orientation), Primitives.Orientation.Horizontal);
            builder.AddComponentParameter(2, nameof(ScrollAreaScrollbar.KeepMounted), true);
            builder.AddComponentParameter(3, nameof(ScrollAreaScrollbar.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<ScrollAreaThumb>(0);
                b.AddComponentParameter(1, nameof(ScrollAreaThumb.ClassValue), (Func<ScrollAreaThumbState, string?>)(s =>
                {
                    seen = s;
                    return null;
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }));

        Assert.Equal(new ScrollAreaThumbState(false, Primitives.Orientation.Horizontal), seen);
    }

    [Fact(DisplayName = "port: the corner renders no element before the first measurement, while a kept-mounted scrollbar does")]
    [Trait("Requirement", "SCR-DOM-26")]
    [Trait("Requirement", "SCR-EDGE-13")]
    public void The_corner_renders_no_element_before_the_first_measurement()
    {
        IRenderedComponent<ScrollAreaRoot> cut = context.Render<ScrollAreaRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<ScrollAreaScrollbar>(0);
            builder.AddComponentParameter(1, nameof(ScrollAreaScrollbar.KeepMounted), true);
            builder.CloseComponent();
            builder.OpenComponent<ScrollAreaCorner>(2);
            builder.CloseComponent();
        }));

        Assert.Single(cut.FindAll("[data-orientation]"));
        Assert.Null(cut.FindComponent<ScrollAreaCorner>().Instance.Element);
        Assert.Empty(cut.FindAll("[aria-hidden]:not([data-orientation])"));
    }

    private IRenderedComponent<ScrollAreaRoot> RenderScrollbar(Action<ComponentParameterCollectionBuilder<ScrollAreaScrollbar>> scrollbar) =>
        context.Render<ScrollAreaRoot>(p => p.AddChildContent<ScrollAreaScrollbar>(s =>
        {
            s.AddUnmatched("data-testid", "scrollbar");
            scrollbar(s);
        }));
}
