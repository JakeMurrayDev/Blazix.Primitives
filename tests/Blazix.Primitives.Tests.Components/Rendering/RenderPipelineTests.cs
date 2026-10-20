using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Consumers;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Rendering;

/// <summary>
/// The pipeline on <see cref="SampleRoot"/> (RND-ALG-01/08, RND-STR-02..06, RND-DOM-14..18, RND-DOM-25/26, RND-EDGE-03/06),
/// with the titles of <c>internals/useRenderElement.test.tsx</c>. The part supplies <c>class="sample-root"</c> and
/// <c>style="padding: 10px"</c>, as the test component of that file supplies <c>test-component</c> and <c>padding: 10px</c>.
/// </summary>
public sealed class RenderPipelineTests : IDisposable
{
    private readonly BunitContext context = new();

    public RenderPipelineTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "accepts className as function")]
    [Trait("Requirement", "RND-DOM-14")]
    public void Accepts_className_as_function()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.ClassValue, (Func<SampleRootState, string?>)(state => state.Open ? "active-class" : "inactive-class")));

        Assert.Equal("active-class sample-root", Root(cut).GetAttribute("class"));
    }

    [Fact(DisplayName = "accepts className as function that returns undefined")]
    [Trait("Requirement", "RND-DOM-17")]
    public void Accepts_className_as_function_that_returns_undefined()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.ClassValue, (Func<SampleRootState, string?>)(state => state.Open ? "active-class" : null)));

        Assert.Equal("sample-root", Root(cut).GetAttribute("class"));
    }

    [Fact(DisplayName = "accepts style as function")]
    [Trait("Requirement", "RND-DOM-16")]
    public void Accepts_style_as_function()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.StyleValue, (Func<SampleRootState, string?>)(state => state.Open ? "color: rgb(255, 0, 0)" : "color: rgb(0, 255, 0)")));

        Assert.Equal("padding: 10px; color: rgb(255, 0, 0);", Root(cut).GetAttribute("style"));
    }

    [Fact(DisplayName = "accepts style as function that returns undefined")]
    [Trait("Requirement", "RND-DOM-18")]
    public void Accepts_style_as_function_that_returns_undefined()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.StyleValue, (Func<SampleRootState, string?>)(state => state.Open ? "color: rgb(255, 0, 0)" : null)));

        Assert.Equal("padding: 10px", Root(cut).GetAttribute("style"));
    }

    [Fact(DisplayName = "the plain class string comes before the class function and both before the part's class")]
    [Trait("Requirement", "RND-DOM-14")]
    public void The_plain_class_string_comes_before_the_class_function_and_both_before_the_parts_class()
    {
        // ADR 02 decision 2 (P1): consumer string, then consumer function, then part classes.
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .AddUnmatched("class", "c")
            .Add(x => x.ClassValue, (Func<SampleRootState, string?>)(_ => "f")));

        Assert.Equal("c f sample-root", Root(cut).GetAttribute("class"));
    }

    [Fact(DisplayName = "the style function's keys override the plain style string's, which override the part's")]
    [Trait("Requirement", "RND-DOM-16")]
    public void The_style_functions_keys_override_the_plain_style_strings_which_override_the_parts()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .AddUnmatched("style", "padding: 2px; color: blue")
            .Add(x => x.StyleValue, (Func<SampleRootState, string?>)(_ => "color: red; margin: 0")));

        Assert.Equal("padding: 2px; color: red; margin: 0;", Root(cut).GetAttribute("style"));
    }

    [Fact(DisplayName = "makes single prop objects preventable")]
    [Trait("Requirement", "RND-API-06")]
    public void Makes_single_prop_objects_preventable()
    {
        int calls = 0;
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .AddUnmatched("onmousedown", EventCallback.Factory.Create<MouseEventArgs>(new Receiver(), e =>
            {
                calls++;
                e.PreventPartHandler();
            })));

        Root(cut).MouseDown();

        Assert.Equal(1, calls);
    }

    [Fact(DisplayName = "makes obscure single-prop events preventable")]
    [Trait("Requirement", "RND-EVT-06")]
    public void Makes_obscure_single_prop_events_preventable()
    {
        int calls = 0;
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .AddUnmatched("oncontextmenu", EventCallback.Factory.Create<MouseEventArgs>(new Receiver(), e =>
            {
                calls++;
                e.PreventPartHandler();
            })));

        Root(cut).ContextMenu();

        Assert.Equal(1, calls);
    }

    [Fact(DisplayName = "does not resolve props when disabled")]
    [Trait("Requirement", "RND-DOM-25")]
    public void Does_not_resolve_props_when_disabled()
    {
        int classCalls = 0;
        int styleCalls = 0;
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.Enabled, false)
            .Add(x => x.ClassValue, (Func<SampleRootState, string?>)(_ =>
            {
                classCalls++;
                return "x";
            }))
            .Add(x => x.StyleValue, (Func<SampleRootState, string?>)(_ =>
            {
                styleCalls++;
                return "color: red";
            })));

        Assert.Equal(string.Empty, cut.Markup);
        Assert.Null(cut.Instance.Element);
        Assert.Equal(0, classCalls);
        Assert.Equal(0, styleCalls);
    }

    [Fact(DisplayName = "handles enabled toggles across rerenders")]
    [Trait("Requirement", "RND-DOM-26")]
    public void Handles_enabled_toggles_across_rerenders()
    {
        int clicks = 0;
        EventCallback<MouseEventArgs> handleClick = EventCallback.Factory.Create<MouseEventArgs>(new Receiver(), _ => clicks++);
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.Enabled, false)
            .AddUnmatched("id", "rerender-target")
            .AddUnmatched("onclick", handleClick));

        Assert.Empty(cut.FindAll("#rerender-target"));
        Assert.Null(cut.Instance.Element);

        cut.Render(p => p.Add(x => x.Enabled, true).AddUnmatched("id", "rerender-target").AddUnmatched("onclick", handleClick));

        IElement element = cut.Find("#rerender-target");
        Assert.NotNull(cut.Instance.Element);
        element.Click();
        Assert.Equal(1, clicks);

        cut.Render(p => p.Add(x => x.Enabled, false).AddUnmatched("id", "rerender-target").AddUnmatched("onclick", handleClick));

        Assert.Empty(cut.FindAll("#rerender-target"));
        Assert.Null(cut.Instance.Element);
    }

    [Fact(DisplayName = "accepts render as a function that receives props and state")]
    [Trait("Requirement", "RND-STR-04")]
    public void Accepts_render_as_a_function_that_receives_props_and_state()
    {
        List<RenderProps<SampleRootState>> renderCalls = [];
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddUnmatched("data-testid", "custom")
            .Add(x => x.RenderFunction, (RenderFragment<RenderProps<SampleRootState>>)(props => builder =>
            {
                renderCalls.Add(props);
                builder.OpenElement(0, "span");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.AddAttribute(2, "data-active", props.State.Open ? "true" : "false");
                builder.CloseElement();
            })));

        Assert.NotEmpty(renderCalls);
        RenderProps<SampleRootState> first = renderCalls[0];
        Assert.Equal("sample-root", first.Attributes["class"]);
        Assert.Equal("custom", first.Attributes["data-testid"]);
        Assert.Equal("padding: 10px", first.Attributes["style"]);
        Assert.True(first.State.Open);
        IElement element = cut.Find("[data-testid=custom]");
        Assert.Equal("SPAN", element.TagName, ignoreCase: true);
        Assert.Equal("true", element.GetAttribute("data-active"));
        Assert.Null(cut.Instance.Element);
    }

    [Fact(DisplayName = "accepts render as a React element and clones it with merged props")]
    [Trait("Requirement", "RND-STR-03")]
    public void Accepts_render_as_a_React_element_and_clones_it_with_merged_props()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddUnmatched("data-testid", "custom")
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenComponent<CustomElement>(0);
                builder.AddComponentParameter(1, "data-active", "true");
                builder.CloseComponent();
            })));

        IElement element = cut.Find("[data-testid=custom]");
        Assert.Equal("SPAN", element.TagName, ignoreCase: true);
        Assert.Equal("true", element.GetAttribute("data-active"));
        Assert.Equal("group", element.GetAttribute("role"));
        Assert.Equal(cut.Instance.ResolvedId, element.GetAttribute("id"));
        Assert.Null(cut.Instance.Element);
    }

    [Fact(DisplayName = "merges className from render element and component props")]
    [Trait("Requirement", "RND-DOM-15")]
    public void Merges_className_from_render_element_and_component_props()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddUnmatched("class", "component-class")
            .Add(x => x.Render, Element("div", ("class", "render-class"))));

        Assert.Equal("render-class component-class sample-root", Root(cut).GetAttribute("class"));
    }

    [Fact(DisplayName = "merges className function with render element")]
    [Trait("Requirement", "RND-DOM-15")]
    public void Merges_className_function_with_render_element()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.ClassValue, (Func<SampleRootState, string?>)(state => state.Open ? "active-class" : ""))
            .Add(x => x.Render, Element("div", ("class", "render-class"))));

        Assert.Equal("render-class active-class sample-root", Root(cut).GetAttribute("class"));
    }

    [Fact(DisplayName = "merges style from render element and component props")]
    [Trait("Requirement", "RND-DOM-16")]
    public void Merges_style_from_render_element_and_component_props()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddUnmatched("style", "color: rgb(255, 0, 0)")
            .Add(x => x.Render, Element("div", ("style", "font-size: 16px"))));

        Assert.Equal("padding: 10px; color: rgb(255, 0, 0); font-size: 16px;", Root(cut).GetAttribute("style"));
    }

    [Fact(DisplayName = "merges style function with render element")]
    [Trait("Requirement", "RND-DOM-16")]
    public void Merges_style_function_with_render_element()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.StyleValue, (Func<SampleRootState, string?>)(state => state.Open ? "color: rgb(255, 0, 0)" : "color: rgb(0, 0, 0)"))
            .Add(x => x.Render, Element("div", ("style", "font-size: 16px"))));

        Assert.Equal("padding: 10px; color: rgb(255, 0, 0); font-size: 16px;", Root(cut).GetAttribute("style"));
    }

    [Fact(DisplayName = "merges props with the same precedence as a plain render element")]
    [Trait("Requirement", "RND-DOM-19")]
    public void Merges_props_with_the_same_precedence_as_a_plain_render_element()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddUnmatched("data-slot", "component")
            .Add(x => x.Render, Element("div", ("class", "render-class"), ("style", "font-size: 16px"), ("data-slot", "render"))));

        IElement element = Root(cut);
        Assert.Equal("render", element.GetAttribute("data-slot"));
        Assert.Contains("render-class", element.ClassList);
        Assert.Contains("sample-root", element.ClassList);
        Assert.Equal("padding: 10px; font-size: 16px;", element.GetAttribute("style"));
    }

    [Fact(DisplayName = "throws error for invalid render element in development")]
    [Trait("Requirement", "RND-EDGE-03")]
    public void Throws_error_for_invalid_render_element_in_development()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            context.Render<SampleRoot>(p => p.Add(x => x.Render, (RenderFragment)(builder => builder.AddContent(0, "not a valid element")))));

        Assert.Contains("Render", error.Message);
        Assert.Contains("element or a component", error.Message);
    }

    [Fact(DisplayName = "a Render fragment with two roots is an invalid substitute")]
    [Trait("Requirement", "RND-EDGE-03")]
    public void A_Render_fragment_with_two_roots_is_an_invalid_substitute()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            context.Render<SampleRoot>(p => p.Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "div");
                builder.CloseElement();
                builder.OpenElement(1, "span");
                builder.CloseElement();
            }))));

        Assert.Contains("exactly one root", error.Message);
    }

    [Fact(DisplayName = "supplying both Render and RenderFunction is an error")]
    [Trait("Requirement", "RND-API-03")]
    public void Supplying_both_Render_and_RenderFunction_is_an_error()
    {
        // ADR 02 decision 1: the two parameters together are Base UI's render; both is a development-time error.
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            context.Render<SampleRoot>(p => p
                .Add(x => x.Render, Element("div"))
                .Add(x => x.RenderFunction, (RenderFragment<RenderProps<SampleRootState>>)(_ => builder => builder.OpenElement(0, "div")))));

        Assert.Contains("RenderFunction", error.Message);
    }

    [Fact(DisplayName = "does not throw when className is provided with minimal props")]
    [Trait("Requirement", "RND-EDGE-06")]
    public void Does_not_throw_when_className_is_provided_with_minimal_props()
    {
        IRenderedComponent<MinimalPart> cut = context.Render<MinimalPart>(p => p.AddUnmatched("class", "test-class"));

        Assert.Equal("test-class", cut.Find("div").GetAttribute("class"));
    }

    [Fact(DisplayName = "does not throw when style is provided with minimal props")]
    [Trait("Requirement", "RND-EDGE-06")]
    public void Does_not_throw_when_style_is_provided_with_minimal_props()
    {
        IRenderedComponent<MinimalPart> cut = context.Render<MinimalPart>(p => p.AddUnmatched("style", "color: red"));

        Assert.Equal("color: red", cut.Find("div").GetAttribute("style"));
    }

    [Fact(DisplayName = "renders the default element with the state data attributes, the part attributes and the child content")]
    [Trait("Requirement", "RND-STR-01")]
    public void Renders_the_default_element_with_the_state_data_attributes_the_part_attributes_and_the_child_content()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.Orientation, Orientation.Vertical)
            .Add(x => x.Label, "hello")
            .AddChildContent("<em>child</em>"));

        IElement root = Root(cut);
        Assert.Equal("DIV", root.TagName, ignoreCase: true);
        Assert.Equal("", root.GetAttribute("data-open"));
        Assert.Equal("vertical", root.GetAttribute("data-orientation"));
        Assert.Equal("hello", root.GetAttribute("data-label"));
        Assert.Equal("group", root.GetAttribute("role"));
        Assert.Equal("<em>child</em>", root.InnerHtml);
        Assert.Single(cut.Nodes.OfType<IElement>());
    }

    [Fact(DisplayName = "a supplied attribute overrides a state data attribute")]
    [Trait("Requirement", "RND-DOM-10")]
    public void A_supplied_attribute_overrides_a_state_data_attribute()
    {
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddUnmatched("data-open", "false"));

        Assert.Equal("false", Root(cut).GetAttribute("data-open"));
    }

    [Fact(DisplayName = "a default button carries type=button unless supplied and a default img carries alt=\"\"")]
    [Trait("Requirement", "RND-STR-05")]
    public void A_default_button_carries_type_button_unless_supplied_and_a_default_img_carries_alt()
    {
        Assert.Equal("button", context.Render<ButtonPart>().Find("button").GetAttribute("type"));
        Assert.Equal("submit", context.Render<ButtonPart>(p => p.AddUnmatched("type", "submit")).Find("button").GetAttribute("type"));
        Assert.Equal("", context.Render<ImagePart>().Find("img").GetAttribute("alt"));
        Assert.Equal("a", context.Render<ImagePart>(p => p.AddUnmatched("alt", "a")).Find("img").GetAttribute("alt"));
    }

    [Fact(DisplayName = "a substitute element's own children replace the part's child content")]
    [Trait("Requirement", "RND-DOM-19")]
    public void A_substitute_elements_own_children_replace_the_parts_child_content()
    {
        IRenderedComponent<SampleRoot> withChildren = context.Render<SampleRoot>(p => p
            .AddChildContent("part child")
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "p");
                builder.AddContent(1, "substitute child");
                builder.CloseElement();
            })));
        IRenderedComponent<SampleRoot> without = context.Render<SampleRoot>(p => p
            .AddChildContent("part child")
            .Add(x => x.Render, Element("p")));

        Assert.Equal("substitute child", Root(withChildren).TextContent);
        Assert.Equal("part child", Root(without).TextContent);
    }

    [Fact(DisplayName = "re-emits every kind of child frame of a substitute element")]
    [Trait("Requirement", "RND-STR-03")]
    public void Re_emits_every_kind_of_child_frame_of_a_substitute_element()
    {
        ElementReference? inner = null;
        CustomElement? child = null;
        IRenderedComponent<SampleRoot> cut = context.Render<SampleRoot>(p => p
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "section");
                builder.AddAttribute(1, "data-slot", "render");
                builder.AddContent(2, "text ");
                builder.AddMarkupContent(3, "<i>markup</i>");
                builder.OpenRegion(4);
                builder.OpenElement(5, "span");
                builder.SetKey("k");
                builder.AddAttribute(6, "class", "nested");
                builder.AddElementReferenceCapture(7, element => inner = element);
                builder.AddContent(8, "nested");
                builder.CloseElement();
                builder.CloseRegion();
                builder.OpenComponent<CustomElement>(9);
                builder.AddComponentParameter(10, "data-active", "yes");
                builder.AddComponentReferenceCapture(11, component => child = (CustomElement)component);
                builder.CloseComponent();
                builder.CloseElement();
            })));

        IElement root = Root(cut);
        Assert.Equal("SECTION", root.TagName, ignoreCase: true);
        Assert.Equal("text <i>markup</i><span class=\"nested\" blazor:elementReference=\"" + inner!.Value.Id + "\">nested</span><span data-active=\"yes\"></span>", root.InnerHtml);
        Assert.NotNull(child);
        Assert.NotNull(cut.Instance.Element);
    }

    [Fact(DisplayName = "a static Razor substitute element is merged from its markup")]
    [Trait("Requirement", "RND-STR-03")]
    public void A_static_Razor_substitute_element_is_merged_from_its_markup()
    {
        IRenderedComponent<StaticMarkupConsumer> cut = context.Render<StaticMarkupConsumer>();

        IElement element = cut.Find("[data-testid=static]");
        Assert.Equal("SPAN", element.TagName, ignoreCase: true);
        Assert.Equal("x c sample-root", element.GetAttribute("class"));
        Assert.Equal("padding: 10px; color: blue; font-size: 16px;", element.GetAttribute("style"));
        Assert.Equal("render", element.GetAttribute("data-slot"));
        Assert.Equal("static", element.GetAttribute("title"));
        Assert.Equal("", element.GetAttribute("data-open"));
        Assert.Equal("group", element.GetAttribute("role"));
        Assert.Equal("inner text", element.TextContent);
    }

    [Fact(DisplayName = "a Razor substitute element with dynamic parts keeps its children and capture")]
    [Trait("Requirement", "RND-STR-03")]
    public void A_Razor_substitute_element_with_dynamic_parts_keeps_its_children_and_capture()
    {
        IRenderedComponent<StaticMarkupConsumer> cut = context.Render<StaticMarkupConsumer>();

        IElement element = cut.Find("[data-testid=dynamic]");
        Assert.Equal("dyn c sample-root", element.GetAttribute("class"));
        Assert.Equal("dynamic text<b>bold</b>", element.InnerHtml.Replace(" blazor:elementReference=\"" + cut.Instance.DynamicElement.Id + "\"", ""));
        Assert.Equal(cut.Instance.DynamicElement.Id, element.GetAttribute("blazor:elementReference"));
    }

    [Fact(DisplayName = "the RenderFunction child-content syntax receives the merged attributes, the state and the child content")]
    [Trait("Requirement", "RND-STR-04")]
    public void The_RenderFunction_child_content_syntax_receives_the_merged_attributes_the_state_and_the_child_content()
    {
        IRenderedComponent<StaticMarkupConsumer> cut = context.Render<StaticMarkupConsumer>();

        IElement element = cut.Find("[data-testid=callback]");
        Assert.Equal("SECTION", element.TagName, ignoreCase: true);
        Assert.Equal("c sample-root", element.GetAttribute("class"));
        Assert.Equal("True", element.GetAttribute("data-open"));
        Assert.Equal("group", element.GetAttribute("role"));
        Assert.Equal("child content", element.TextContent.Trim());
    }

    [Fact(DisplayName = "a static substitute without children renders the part's child content inside it")]
    [Trait("Requirement", "RND-STR-03")]
    public void A_static_substitute_without_children_renders_the_parts_child_content_inside_it()
    {
        IRenderedComponent<StaticMarkupConsumer> cut = context.Render<StaticMarkupConsumer>();

        IElement element = cut.Find("[data-testid=with-children]");
        Assert.Equal("P", element.TagName, ignoreCase: true);
        Assert.Equal("part child", element.TextContent);
    }

    [Fact(DisplayName = "static markup with two roots is an invalid substitute")]
    [Trait("Requirement", "RND-EDGE-03")]
    public void Static_markup_with_two_roots_is_an_invalid_substitute()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            context.Render<StaticMarkupConsumer>(p => p.Add(x => x.TwoRoots, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div></div><span></span>")))));

        Assert.Contains("exactly one root", error.Message);
    }

    private static IElement Root(IRenderedComponent<SampleRoot> cut) => cut.Nodes.OfType<IElement>().Single();

    private static RenderFragment Element(string tag, params (string Name, string Value)[] attributes)
    {
        return builder =>
        {
            builder.OpenElement(0, tag);
            for (int i = 0; i < attributes.Length; i++)
            {
                builder.AddAttribute(1, attributes[i].Name, attributes[i].Value);
            }

            builder.CloseElement();
        };
    }

    /// <summary>Base UI's <c>CustomElement</c>: a consumer component that spreads what it receives onto a <c>span</c>.</summary>
    public sealed class CustomElement : ComponentBase
    {
        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object>? Attributes { get; set; }

        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddMultipleAttributes(1, Attributes);
            builder.AddContent(2, ChildContent);
            builder.CloseElement();
        }
    }

    private sealed class Receiver : IHandleEvent
    {
        public Task HandleEventAsync(EventCallbackWorkItem item, object? arg) => item.InvokeAsync(arg);
    }
}
