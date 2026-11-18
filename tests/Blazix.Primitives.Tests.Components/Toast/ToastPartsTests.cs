using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// <c>toast/utils/isRenderableNode.test.ts</c> restated over the port's content, a string or a fragment (TST-DOM-16; the
/// dated note on the row): a null or empty string and no fragment render nothing, any other string (<c>"0"</c> included)
/// and any fragment do. Base UI's arrays have no C# form, and a fragment is opaque, so "recurses into arrays" is named
/// with that reason; "requires an element whose children are renderable" is the pipeline's content gate
/// (<c>Rendering/RequiresContentTests</c>, ADR 02 Amendments 2026-11-18).
/// </summary>
public sealed class ToastRenderableContentTests
{
    [Fact(DisplayName = "treats renderable primitives as content")]
    [Trait("Requirement", "TST-DOM-16")]
    public void Treats_renderable_primitives_as_content()
    {
        ToastLabelBehavior label = new(new Utilities.LabelIdSlot());

        Assert.NotNull(label.Content(null, null, "0"));
        Assert.NotNull(label.Content(null, null, "NaN"));
        Assert.NotNull(label.Content(null, null, "text"));
        Assert.NotNull(label.Content(null, Fragment, null));
        Assert.NotNull(label.Content(Fragment, null, null));
    }

    [Fact(DisplayName = "treats non-rendering values as empty")]
    [Trait("Requirement", "TST-DOM-16")]
    public void Treats_non_rendering_values_as_empty()
    {
        ToastLabelBehavior label = new(new Utilities.LabelIdSlot());

        Assert.Null(label.Content(null, null, null));
        Assert.Null(label.Content(null, null, string.Empty));
    }

    private static void Fragment(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) => builder.AddContent(0, "x");
}

/// <summary>
/// Action, Close and Content at L2: <c>ToastAction.test.tsx</c>, <c>ToastClose.test.tsx</c> and
/// <c>ToastContent.test.tsx</c>, every title verbatim, and the C# writer of the close button's <c>aria-hidden</c>
/// (TST-ARIA-07; the M6 plan's decision 19 (a)). The stack's expanded flag the hover sets is the module's report (M6 track
/// 9b); here the store is written as the report would write it.
/// </summary>
public sealed class ToastPartsTests : IDisposable
{
    private readonly ToastTestContext test = new();

    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "performs an action when clicked")]
    [Trait("Requirement", "TST-PTR-27")]
    [Trait("Requirement", "TST-EVT-09")]
    public void Performs_an_action_when_clicked()
    {
        int clicks = 0;
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () => test.Manager.Add(new ToastObject
        {
            Title = "title",
            ActionContent = Text("action"),
            ActionAttributes = new Dictionary<string, object> { ["onclick"] = EventCallback.Factory.Create<MouseEventArgs>(this, () => clicks++) },
        }));
        Assert.Equal(0, clicks);

        cut.Find("[data-testid=action]").Click();

        Assert.Equal(1, clicks);
        Assert.Equal("action", cut.Find("[data-testid=action]").TextContent);
        Assert.NotEqual(TransitionStatus.Ending, test.Manager.Toasts[0].TransitionStatus);
    }

    [Fact(DisplayName = "forwards actionProps to the button")]
    [Trait("Requirement", "TST-DOM-19")]
    [Trait("Requirement", "TST-EVT-09")]
    public void Forwards_actionProps_to_the_button()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();

        test.Run(cut, () => test.Manager.Add(new ToastObject
        {
            Title = "title",
            ActionContent = Text("action"),
            ActionAttributes = new Dictionary<string, object> { ["id"] = "action", ["data-testid"] = "action" },
        }));

        Assert.Equal("action", cut.Find("[data-testid=action]").Id);
    }

    [Fact(DisplayName = "does not render if it has no children")]
    [Trait("Requirement", "TST-STR-07")]
    [Trait("Requirement", "TST-DOM-19")]
    public void Does_not_render_if_it_has_no_children()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();

        test.Run(cut, () => test.Manager.Add(new ToastObject { ActionContent = null }));

        Assert.NotNull(cut.Find("[data-testid=root]"));
        Assert.Empty(cut.FindAll("[data-testid=action]"));
    }

    [Fact(DisplayName = "renders content passed through the render prop")]
    [Trait("Requirement", "TST-DOM-19")]
    public void Renders_content_passed_through_the_render_prop()
    {
        IRenderedComponent<ToastActionHost> cut = test.Context.Render<ToastActionHost>(p => p.Add(x => x.Form, "render-prop"));

        Assert.Equal("render prop action", cut.Find("button").TextContent);
    }

    [Fact(DisplayName = "does not render a childless render prop when there is no action content")]
    [Trait("Requirement", "TST-DOM-19")]
    public void Does_not_render_a_childless_render_prop_when_there_is_no_action_content()
    {
        IRenderedComponent<ToastActionHost> cut = test.Context.Render<ToastActionHost>(p => p.Add(x => x.Form, "childless"));

        Assert.Empty(cut.FindAll("[data-testid=action-render]"));
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact(DisplayName = "renders the toast action content through a childless render prop")]
    [Trait("Requirement", "TST-DOM-19")]
    public void Renders_the_toast_action_content_through_a_childless_render_prop()
    {
        IRenderedComponent<ToastActionHost> cut = test.Context.Render<ToastActionHost>(p => p
            .Add(x => x.Form, "childless")
            .Add(x => x.Toast, new ToastObject { Id = "test", Title = "title", ActionContent = Text("Toast action") }));

        Assert.Equal("Toast action", cut.Find("[data-testid=action-render]").TextContent);
    }

    [Fact(DisplayName = "port: the toast's action attributes win over the part's, and their handlers run together")]
    [Trait("Requirement", "TST-DOM-19")]
    [Trait("Requirement", "TST-EVT-09")]
    public void The_toasts_action_attributes_win_over_the_parts_and_their_handlers_run_together()
    {
        List<string> calls = [];
        IRenderedComponent<ToastActionHost> cut = test.Context.Render<ToastActionHost>(p => p
            .Add(x => x.Form, "attributes")
            .Add(x => x.OnPartClick, () => calls.Add("part"))
            .Add(x => x.Toast, new ToastObject
            {
                Id = "test",
                ActionContent = Text("go"),
                ActionAttributes = new Dictionary<string, object>
                {
                    ["id"] = "from-toast",
                    ["onclick"] = EventCallback.Factory.Create<MouseEventArgs>(this, () => calls.Add("toast")),
                },
            }));

        IElement button = cut.Find("button");
        Assert.Equal("from-toast", button.Id);
        Assert.Equal("part-title", button.GetAttribute("title"));
        button.Click();

        // Base UI's mergeProps runs the rightmost handler first: the toast's, then the part's.
        Assert.Equal(["toast", "part"], calls);
    }

    [Fact(DisplayName = "closes the toast when clicked")]
    [Trait("Requirement", "TST-PTR-26")]
    public void Closes_the_toast_when_clicked()
    {
        int closed = 0;
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "title", OnClose = () => closed++ }));

        cut.Find("[data-testid=close]").Click();

        // The removal after the exit is the root's completion watcher (M6 track 9b).
        Assert.Equal(1, closed);
        Assert.Equal(TransitionStatus.Ending, test.Manager.Toasts[0].TransitionStatus);
    }

    [Fact(DisplayName = "unmarks toasts as limited when the limit is not exceeded")]
    [Trait("Requirement", "TST-PTR-26")]
    [Trait("Requirement", "TST-STA-04")]
    public void Unmarks_toasts_as_limited_when_the_limit_is_not_exceeded()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost(p => p.Add(x => x.Limit, 2));
        for (int i = 1; i <= 3; i++)
        {
            int count = i;
            test.Run(cut, () => test.Manager.Add(new ToastObject { Id = $"toast-{count}", Title = $"toast-{count}" }));
        }

        Assert.True(ToastTestContext.Roots(cut)[2].HasAttribute("data-limited"));

        cut.FindAll("[data-testid=close]")[0].Click();

        Assert.False(ToastTestContext.Roots(cut)[2].HasAttribute("data-limited"));
        Assert.False(ToastTestContext.Roots(cut)[2].HasAttribute("inert"));
    }

    [Fact(DisplayName = "port: a disabled close does not close its toast")]
    [Trait("Requirement", "TST-PTR-26")]
    [Trait("Requirement", "TST-API-16")]
    public void A_disabled_close_does_not_close_its_toast()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost(p => p.Add(x => x.RootContent, _ => builder =>
        {
            builder.OpenComponent<ToastClose>(0);
            builder.AddComponentParameter(1, nameof(ToastClose.Disabled), true);
            builder.AddAttribute(2, "data-testid", "close");
            builder.CloseComponent();
        }));
        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "title" }));

        cut.Find("[data-testid=close]").Click();

        Assert.NotEqual(TransitionStatus.Ending, test.Manager.Toasts[0].TransitionStatus);
        Assert.True(cut.Find("[data-testid=close]").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "port: the close button is aria-hidden while the stack is collapsed and it does not have focus")]
    [Trait("Requirement", "TST-ARIA-07")]
    public void The_close_button_is_aria_hidden_while_the_stack_is_collapsed_and_it_does_not_have_focus()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "title" }));
        ToastStore<NoPayload> store = cut.FindComponent<ToastProvider<NoPayload>>().Instance.Store;
        Assert.Equal("true", cut.Find("[data-testid=close]").GetAttribute("aria-hidden"));

        // The hover the module reports expands the stack.
        test.Run(cut, () => store.SetHovering(true));
        Assert.Equal("false", cut.Find("[data-testid=close]").GetAttribute("aria-hidden"));

        test.Run(cut, () => store.SetHovering(false));
        Assert.Equal("true", cut.Find("[data-testid=close]").GetAttribute("aria-hidden"));

        // Its own focus keeps it exposed.
        cut.Find("[data-testid=close]").Focus();
        Assert.Equal("false", cut.Find("[data-testid=close]").GetAttribute("aria-hidden"));
        cut.Find("[data-testid=close]").Blur();
        Assert.Equal("true", cut.Find("[data-testid=close]").GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "marks content behind the frontmost toast with data-behind")]
    [Trait("Requirement", "TST-DOM-15")]
    [Trait("Requirement", "TST-STA-01")]
    [Trait("Requirement", "TST-STA-07")]
    public void Marks_content_behind_the_frontmost_toast_with_data_behind()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();

        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "toast-1" }));
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "toast-2" }));

        // The newest toast is at the front; the older one sits behind it.
        IReadOnlyList<IElement> contents = cut.FindAll("[data-testid=content]");
        Assert.False(contents[0].HasAttribute("data-behind"));
        Assert.True(contents[1].HasAttribute("data-behind"));
    }

    [Fact(DisplayName = "reflects the expanded state when the viewport is hovered")]
    [Trait("Requirement", "TST-DOM-15")]
    [Trait("Requirement", "TST-DOM-06")]
    public void Reflects_the_expanded_state_when_the_viewport_is_hovered()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "toast-1" }));
        ToastStore<NoPayload> store = cut.FindComponent<ToastProvider<NoPayload>>().Instance.Store;
        Assert.False(cut.Find("[data-testid=content]").HasAttribute("data-expanded"));

        // The mouseenter the module reports (M6 track 9b; the re-tag of TST-PTR-01).
        test.Run(cut, () => store.SetHovering(true));

        Assert.True(cut.Find("[data-testid=content]").HasAttribute("data-expanded"));
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-expanded"));
    }

    [Fact(DisplayName = "port: a closing toast's content is never behind")]
    [Trait("Requirement", "TST-DOM-15")]
    public void A_closing_toasts_content_is_never_behind()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "old" }));
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "new" }));
        Assert.True(cut.FindAll("[data-testid=content]")[1].HasAttribute("data-behind"));

        test.Run(cut, () => test.Manager.Close("old"));

        Assert.False(cut.FindAll("[data-testid=content]")[1].HasAttribute("data-behind"));
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <Toast.Provider>")]
    [Trait("Requirement", "TST-STR-02")]
    [Trait("Requirement", "TST-EDGE-01")]
    public void Throws_a_descriptive_error_when_rendered_outside_ToastProvider()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<ToastViewport<NoPayload>>());

        Assert.Equal("Blazix: `ToastProviderContext` is missing. Toast parts must be used within `<ToastProvider>`.", error.Message);
    }

    [Fact(DisplayName = "port: the viewport renders its region and live-region attributes, each overridable by the consumer")]
    [Trait("Requirement", "TST-ARIA-01")]
    [Trait("Requirement", "TST-DOM-01")]
    [Trait("Requirement", "TST-DOM-23")]
    public void The_viewport_renders_its_region_and_live_region_attributes_each_overridable_by_the_consumer()
    {
        IRenderedComponent<ToastProvider<NoPayload>> cut = test.Context.Render<ToastProvider<NoPayload>>(p => p.AddChildContent<ToastViewport<NoPayload>>(v => v
            .AddUnmatched("aria-label", "Alerts")
            .AddUnmatched("data-testid", "viewport")));

        IElement viewport = cut.Find("[data-testid=viewport]");
        Assert.Equal("-1", viewport.GetAttribute("tabindex"));
        Assert.Equal("region", viewport.GetAttribute("role"));
        Assert.Equal("polite", viewport.GetAttribute("aria-live"));
        Assert.Equal("false", viewport.GetAttribute("aria-atomic"));
        Assert.Equal("additions text", viewport.GetAttribute("aria-relevant"));
        Assert.Equal("Alerts", viewport.GetAttribute("aria-label"));
    }

    private static RenderFragment Text(string text) => builder => builder.AddContent(0, text);
}
