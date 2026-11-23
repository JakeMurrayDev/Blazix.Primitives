using AngleSharp.Dom;
using Iodine.Headless.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Family tests use the project's root namespace (tests/Iodine.Headless.Tests.Components/CONTEXT.md).
namespace Iodine.Headless.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>DrawerPopup.test.tsx</c> that need nothing of the <c>drawer</c> module (titles verbatim;
/// DRW-EDGE-01, EDGE-03, STA-11, STA-12, DOM-03, DOM-08, DOM-10), and the port titles of the inline style C# writes (DOM-15)
/// and the popup's id (DOM-16). The heights are the module's measurements (8b): here the test reports them into the roots'
/// internal <see cref="DrawerRootContext"/> as the module's twin will, inside the renderer's dispatcher, and reads the parent
/// popup in the batch that report renders. Base UI reads <c>style.getPropertyValue</c>; the inline declaration C# writes is
/// the same value.
/// </summary>
public sealed class DrawerPopupTests : IDisposable
{
    private const string ViewportWarning = "Iodine: `<DrawerPopup>` expected to be rendered within `<DrawerViewport>`. Omitting the viewport disables drawer swipe handling and touch scroll locking. Wrap `<DrawerPopup>` in `<DrawerViewport>`.";

    private readonly DialogTestContext test = new();
    private readonly CapturingLogger<DrawerPopup> popupLogger = new();

    public DrawerPopupTests()
    {
        test.Context.Services.AddSingleton<ILogger<DrawerPopup>>(popupLogger);
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // Base UI's "warns without relying on React owner-stack support" is not ported: Blazor has no owner stack to append, so
    // this title's exact-message assertion already covers it.
    [Fact(DisplayName = "warns in development when not rendered within a viewport")]
    [Trait("Requirement", "DRW-EDGE-01")]
    public void Warns_in_development_when_not_rendered_within_a_viewport()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(OpenDrawer(withViewport: false, Popup()));
        Assert.Single(cut.FindAll("[role=dialog]"));

        (LogLevel level, string message) = Assert.Single(popupLogger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Equal(ViewportWarning, message);

        // Once, after the first render only.
        cut.Render();
        Assert.Single(popupLogger.Entries);
    }

    [Fact(DisplayName = "port: does not warn when the popup is rendered within a viewport")]
    [Trait("Requirement", "DRW-EDGE-01")]
    public void Does_not_warn_when_the_popup_is_rendered_within_a_viewport()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(OpenDrawer(withViewport: true, Popup()));

        Assert.Single(cut.FindAll("[role=dialog]"));
        Assert.DoesNotContain(popupLogger.Entries, entry => entry.Message.Contains("DrawerViewport", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "does not throw when a popup render function produces no element")]
    [Trait("Requirement", "DRW-EDGE-03")]
    public void Does_not_throw_when_a_popup_render_function_produces_no_element()
    {
        RenderFragment popup = builder =>
        {
            builder.OpenComponent<DrawerPopup>(0);
            builder.AddComponentParameter(1, nameof(DrawerPopup.RenderFunction), (RenderFragment<RenderProps<DrawerPopupState>>)(props => empty => { }));
            builder.AddComponentParameter(2, nameof(DrawerPopup.ChildContent), (RenderFragment)(content => content.AddContent(0, "Drawer")));
            builder.CloseComponent();
        };

        IRenderedComponent<IComponent> cut = test.Context.Render(OpenDrawer(withViewport: true, popup, modal: false));

        Assert.Single(cut.FindAll("[data-testid=viewport]"));
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    // The border-inclusive offsetHeight the popup measures is the `drawer` module's (L4); the reported height is the child's
    // measured 100px plus its two 2px borders.
    [Fact(DisplayName = "includes border size in frontmost height CSS variable for nested drawers")]
    [Trait("Requirement", "DRW-STA-11")]
    [Trait("Requirement", "DRW-DOM-10")]
    public async Task Includes_border_size_in_frontmost_height_CSS_variable_for_nested_drawers()
    {
        IRenderedComponent<DrawerNestingHarness> cut = test.Context.Render<DrawerNestingHarness>(p => p.Add(x => x.ChildOpen, true));
        DrawerRootContext parent = cut.Instance.Parent!.DrawerContext;
        DrawerRootContext child = cut.Instance.Child!.DrawerContext;
        Assert.Same(parent, child.Parent);
        Assert.DoesNotContain(DrawerPopupCssVars.FrontmostHeight, Style(cut.Find("[data-testid=parent-popup]")), StringComparison.Ordinal);

        await cut.InvokeAsync(() =>
        {
            child.ReportPopupHeight(104);
            parent.ReportNestedFrontmostHeight(child.FrontmostHeight);
        });

        Assert.Equal("104px", Variable(cut.Find("[data-testid=parent-popup]"), DrawerPopupCssVars.FrontmostHeight));
        Assert.Equal("104px", Variable(cut.Find("[data-testid=child-popup]"), DrawerPopupCssVars.FrontmostHeight));
    }

    // Base UI's probes record that the child's reports reach the parent before React's passive effects; the port's reports are
    // the module's twin calls (8b), so the test makes them and reads the parent popup in the batch they render: one render
    // of the parent popup, with the new values.
    [Fact(DisplayName = "reports nested presence and frontmost height before passive effects")]
    [Trait("Requirement", "DRW-STA-11")]
    [Trait("Requirement", "DRW-STA-12")]
    [Trait("Requirement", "DRW-DOM-03")]
    [Trait("Requirement", "DRW-DOM-08")]
    [Trait("Requirement", "DRW-DOM-10")]
    public async Task Reports_nested_presence_and_frontmost_height_before_passive_effects()
    {
        int parentRenders = 0;
        IRenderedComponent<DrawerNestingHarness> cut = test.Context.Render<DrawerNestingHarness>(p => p
            .Add(x => x.ChildOpen, false)
            .Add(x => x.ParentClassValue, _ =>
            {
                parentRenders++;
                return null;
            }));
        Assert.Equal("0", Variable(cut.Find("[data-testid=parent-popup]"), DrawerPopupCssVars.NestedDrawers));

        cut.Render(p => p.Add(x => x.ChildOpen, true));
        DrawerRootContext parent = cut.Instance.Parent!.DrawerContext;
        DrawerRootContext child = cut.Instance.Child!.DrawerContext;
        int before = parentRenders;

        await cut.InvokeAsync(() =>
        {
            child.ReportPopupHeight(100);
            parent.ReportNestedFrontmostHeight(child.FrontmostHeight);
            parent.ReportNestedPresence(true);
        });

        Assert.Equal(before + 1, parentRenders);
        Assert.True(parent.HasNestedDrawer);
        IElement parentPopup = cut.Find("[data-testid=parent-popup]");
        Assert.Equal("1", Variable(parentPopup, DrawerPopupCssVars.NestedDrawers));
        Assert.Equal(string.Empty, parentPopup.GetAttribute(DrawerPopupDataAttributes.NestedDrawerOpen));
        Assert.Equal("100px", Variable(parentPopup, DrawerPopupCssVars.FrontmostHeight));
    }

    // DRW-STA-11: a report of 0 reverts the frontmost height to the root's own popup height.
    [Fact(DisplayName = "port: the frontmost height reverts to the own popup height when the nested drawer reports 0")]
    [Trait("Requirement", "DRW-STA-11")]
    [Trait("Requirement", "DRW-DOM-10")]
    public async Task The_frontmost_height_reverts_to_the_own_popup_height()
    {
        IRenderedComponent<DrawerNestingHarness> cut = test.Context.Render<DrawerNestingHarness>(p => p.Add(x => x.ChildOpen, true));
        DrawerRootContext parent = cut.Instance.Parent!.DrawerContext;

        await cut.InvokeAsync(() => parent.ReportPopupHeight(300));
        Assert.Equal("300px", Variable(cut.Find("[data-testid=parent-popup]"), DrawerPopupCssVars.FrontmostHeight));

        await cut.InvokeAsync(() => parent.ReportNestedFrontmostHeight(120));
        Assert.Equal("120px", Variable(cut.Find("[data-testid=parent-popup]"), DrawerPopupCssVars.FrontmostHeight));

        await cut.InvokeAsync(() => parent.ReportNestedFrontmostHeight(0));
        Assert.Equal("300px", Variable(cut.Find("[data-testid=parent-popup]"), DrawerPopupCssVars.FrontmostHeight));
    }

    [Fact(DisplayName = "port: the popup's inline style declares --nested-drawers, then --drawer-frontmost-height, then the consumer's style")]
    [Trait("Requirement", "DRW-DOM-15")]
    [Trait("Requirement", "DRW-DOM-08")]
    [Trait("Requirement", "DRW-DOM-10")]
    public async Task The_popups_inline_style_order()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(OpenDrawer(withViewport: true, Popup(("style", "color: green"))));
        string style = Style(cut.Find("[role=dialog]"));
        Assert.True(style.IndexOf("--nested-drawers: 0", StringComparison.Ordinal) < style.IndexOf("color: green", StringComparison.Ordinal));
        Assert.DoesNotContain(DrawerPopupCssVars.FrontmostHeight, style, StringComparison.Ordinal);

        DrawerRootContext root = cut.FindComponent<DrawerRoot<NoPayload>>().Instance.DrawerContext;
        await cut.InvokeAsync(() => root.ReportPopupHeight(250));

        style = Style(cut.Find("[role=dialog]"));
        int count = style.IndexOf("--nested-drawers: 0", StringComparison.Ordinal);
        int height = style.IndexOf("--drawer-frontmost-height: 250px", StringComparison.Ordinal);
        int consumer = style.IndexOf("color: green", StringComparison.Ordinal);
        Assert.True(count >= 0 && count < height && height < consumer, style);
        Assert.DoesNotContain("--nested-dialogs", style, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: the popup renders the consumer's id when given, and its trigger's aria-controls names it")]
    [Trait("Requirement", "DRW-DOM-16")]
    [Trait("Requirement", "DRW-ARIA-03")]
    public async Task The_popup_renders_the_consumers_id_when_given()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer(open: false, trigger: true, withViewport: true, Popup(("id", "my-popup"))));

        await test.PressAsync(cut, "open", open: true);

        IElement popup = cut.Find("[role=dialog]");
        Assert.Equal("my-popup", popup.Id);
        Assert.Equal("-1", popup.GetAttribute("tabindex"));
        Assert.Equal("my-popup", cut.Find("#open").GetAttribute("aria-controls"));
    }

    // Base UI's cited title ("applies a negative snap point offset to upward drawers") is browser-only (8b); this one reads the
    // attribute alone, for the default and for each member.
    [Theory(DisplayName = "port: the popup renders data-swipe-direction from the root's swipe direction, down by default")]
    [InlineData(null, "down")]
    [InlineData(SwipeDirection.Up, "up")]
    [InlineData(SwipeDirection.Down, "down")]
    [InlineData(SwipeDirection.Left, "left")]
    [InlineData(SwipeDirection.Right, "right")]
    [Trait("Requirement", "DRW-API-01")]
    [Trait("Requirement", "DRW-DOM-05")]
    public void The_popup_renders_data_swipe_direction(SwipeDirection? direction, string expected)
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(OpenDrawer(withViewport: true, Popup(), swipeDirection: direction));

        Assert.Equal(expected, cut.Find("[role=dialog]").GetAttribute(DrawerPopupDataAttributes.SwipeDirection));
    }

    [Fact(DisplayName = "port: the popup's state carries the resolved swipe direction")]
    [Trait("Requirement", "DRW-STA-01")]
    public void The_popups_state_carries_the_resolved_swipe_direction()
    {
        DrawerPopupState? seen = null;
        RenderFragment popup = builder =>
        {
            builder.OpenComponent<DrawerPopup>(0);
            builder.AddComponentParameter(1, nameof(DrawerPopup.ClassValue), (Func<DrawerPopupState, string?>)(state =>
            {
                seen = state;
                return null;
            }));
            builder.CloseComponent();
        };

        test.Context.Render(OpenDrawer(withViewport: true, popup, swipeDirection: SwipeDirection.Left));

        Assert.Equal(SwipeDirection.Left, seen!.SwipeDirection);
    }

    // A DrawerPopup with the given attributes.
    private static RenderFragment Popup(params (string Name, object Value)[] attributes) => builder =>
    {
        builder.OpenComponent<DrawerPopup>(0);
        foreach ((string name, object value) in attributes)
        {
            builder.AddAttribute(1, name, value);
        }

        builder.AddComponentParameter(2, nameof(DrawerPopup.ChildContent), (RenderFragment)(content => content.AddContent(0, "Drawer")));
        builder.CloseComponent();
    };

    // Root open > Portal > (Viewport >) popup, as Base UI's trees.
    private static RenderFragment OpenDrawer(bool withViewport, RenderFragment popup, bool? modal = null, SwipeDirection? swipeDirection = null) =>
        Drawer(open: true, trigger: false, withViewport, popup, modal, swipeDirection);

    private static RenderFragment Drawer(bool open, bool trigger, bool withViewport, RenderFragment popup, bool? modal = null, SwipeDirection? swipeDirection = null) => builder =>
    {
        builder.OpenComponent<DrawerRoot<NoPayload>>(0);
        if (open)
        {
            builder.AddComponentParameter(1, nameof(DrawerRoot<NoPayload>.Open), true);
        }

        if (swipeDirection is { } direction)
        {
            builder.AddComponentParameter(4, nameof(DrawerRoot<NoPayload>.SwipeDirection), (SwipeDirection?)direction);
        }

        if (modal is { } value)
        {
            builder.AddComponentParameter(2, nameof(DrawerRoot<NoPayload>.Modal), (ModalValue?)value);
        }

        builder.AddComponentParameter(3, nameof(DrawerRoot<NoPayload>.ChildContent), (RenderFragment)(root =>
        {
            if (trigger)
            {
                root.OpenComponent<DrawerTrigger<NoPayload>>(0);
                root.AddAttribute(1, "id", "open");
                root.CloseComponent();
            }

            root.OpenComponent<DrawerPortal>(2);
            root.AddComponentParameter(3, nameof(DrawerPortal.ChildContent), (RenderFragment)(portal =>
            {
                if (withViewport)
                {
                    portal.OpenComponent<DrawerViewport>(0);
                    portal.AddAttribute(1, "data-testid", "viewport");
                    portal.AddComponentParameter(2, nameof(DrawerViewport.ChildContent), popup);
                    portal.CloseComponent();
                }
                else
                {
                    portal.AddContent(3, popup);
                }
            }));
            root.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private static string Style(IElement element) => element.GetAttribute("style") ?? string.Empty;

    // The value of one declaration of an element's inline style, which C# writes in the commit path.
    internal static string? Variable(IElement element, string name)
    {
        string style = Style(element);
        string prefix = name + ": ";
        int start = style.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        int end = style.IndexOf(';', start);
        return (end < 0 ? style[(start + prefix.Length)..] : style[(start + prefix.Length)..end]).Trim();
    }
}
