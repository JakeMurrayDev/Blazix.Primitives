using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Rendering;

/// <summary>The public pipeline (RND §3.2, RND-STR-08/09, RND-API-07, RND-STA-02/03, RND-DOM-10), with the titles of <c>use-render/useRender.test.tsx</c>.</summary>
public sealed class RenderElementTests : IDisposable
{
    private readonly BunitContext context = new();

    public RenderElementTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "render props does not overwrite className in a render function when unspecified")]
    [Trait("Requirement", "RND-STR-04")]
    public void Render_props_does_not_overwrite_className_in_a_render_function_when_unspecified()
    {
        IRenderedComponent<RenderElement<EmptyState>> cut = context.Render<RenderElement<EmptyState>>(p => p
            .Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.AddAttribute(2, "class", "my-span " + (props.Attributes.TryGetValue("class", out object? value) ? value : ""));
                builder.CloseElement();
            })));

        Assert.Equal("my-span ", Root(cut).GetAttribute("class"));
    }

    [Fact(DisplayName = "renders div by default if no defaultTagName and no render params are provided")]
    [Trait("Requirement", "RND-STR-08")]
    public void Renders_div_by_default_if_no_defaultTagName_and_no_render_params_are_provided()
    {
        IRenderedComponent<RenderElement<EmptyState>> cut = context.Render<RenderElement<EmptyState>>();

        Assert.Equal("DIV", Root(cut).TagName, ignoreCase: true);
    }

    [Fact(DisplayName = "renders the element with the default tag with no render prop")]
    [Trait("Requirement", "RND-STR-08")]
    public void Renders_the_element_with_the_default_tag_with_no_render_prop()
    {
        IRenderedComponent<RenderElement<EmptyState>> cut = context.Render<RenderElement<EmptyState>>(p => p.Add(x => x.DefaultTagName, "div"));
        Assert.Equal("DIV", Root(cut).TagName, ignoreCase: true);

        cut.Render(p => p.Add(x => x.DefaultTagName, "span"));
        Assert.Equal("SPAN", Root(cut).TagName, ignoreCase: true);
    }

    [Fact(DisplayName = "is overwritten by the render prop")]
    [Trait("Requirement", "RND-STR-09")]
    public void Is_overwritten_by_the_render_prop()
    {
        RenderFragment span = builder =>
        {
            builder.OpenElement(0, "span");
            builder.CloseElement();
        };
        IRenderedComponent<RenderElement<EmptyState>> cut = context.Render<RenderElement<EmptyState>>(p => p.Add(x => x.DefaultTagName, "div").Add(x => x.Render, span));
        Assert.Equal("SPAN", Root(cut).TagName, ignoreCase: true);

        cut.Render(p => p.Add(x => x.DefaultTagName, "a").Add(x => x.Render, span));
        Assert.Equal("SPAN", Root(cut).TagName, ignoreCase: true);
    }

    [Fact(DisplayName = "converts state to data attributes automatically")]
    [Trait("Requirement", "RND-STA-03")]
    public void Converts_state_to_data_attributes_automatically()
    {
        IRenderedComponent<RenderElement<ActiveIndexState>> cut = context.Render<RenderElement<ActiveIndexState>>(p => p
            .Add(x => x.Render, Button())
            .Add(x => x.State, new ActiveIndexState(Active: true, Index: 42)));

        IElement button = Root(cut);
        Assert.Equal("", button.GetAttribute("data-active"));
        Assert.Equal("42", button.GetAttribute("data-index"));
    }

    [Fact(DisplayName = "merges state-based data attributes with existing props")]
    [Trait("Requirement", "RND-DOM-10")]
    public void Merges_state_based_data_attributes_with_existing_props()
    {
        IRenderedComponent<RenderElement<FormState>> cut = context.Render<RenderElement<FormState>>(p => p
            .Add(x => x.Render, Button())
            .Add(x => x.State, new FormState(Form: "login"))
            .Add(x => x.Props, new Dictionary<string, object>
            {
                ["class"] = "btn-primary",
                ["id"] = "submit-btn",
                ["data-existing"] = "prop",
            }));

        IElement button = Root(cut);
        Assert.Equal("login", button.GetAttribute("data-form"));
        Assert.Equal("btn-primary", button.GetAttribute("class"));
        Assert.Equal("submit-btn", button.GetAttribute("id"));
        Assert.Equal("prop", button.GetAttribute("data-existing"));
    }

    [Fact(DisplayName = "props override state-based data attributes")]
    [Trait("Requirement", "RND-DOM-10")]
    public void Props_override_state_based_data_attributes()
    {
        IRenderedComponent<RenderElement<ActiveIndexState>> cut = context.Render<RenderElement<ActiveIndexState>>(p => p
            .Add(x => x.Render, Button())
            .Add(x => x.State, new ActiveIndexState(Active: true, Index: 0))
            .Add(x => x.Props, new Dictionary<string, object> { ["data-active"] = "false" }));

        Assert.Equal("false", Root(cut).GetAttribute("data-active"));
    }

    [Fact(DisplayName = "handles empty state")]
    [Trait("Requirement", "RND-STA-02")]
    public void Handles_empty_state()
    {
        IRenderedComponent<RenderElement<EmptyState>> cut = context.Render<RenderElement<EmptyState>>(p => p
            .Add(x => x.Render, Span())
            .Add(x => x.State, EmptyState.Instance)
            .Add(x => x.Props, new Dictionary<string, object> { ["class"] = "test-class" }));

        IElement span = Root(cut);
        Assert.Equal("test-class", span.GetAttribute("class"));
        Assert.DoesNotContain(span.Attributes, a => a.Name.StartsWith("data-", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "handles undefined state")]
    [Trait("Requirement", "RND-STA-02")]
    public void Handles_undefined_state()
    {
        // Base UI's `state: undefined` is an element with no state: in C# the state type is EmptyState and State is omitted.
        IRenderedComponent<RenderElement<EmptyState>> cut = context.Render<RenderElement<EmptyState>>(p => p
            .Add(x => x.Props, new Dictionary<string, object> { ["class"] = "test-class", ["data-from-props"] = "value" }));

        IElement div = Root(cut);
        Assert.Equal("DIV", div.TagName, ignoreCase: true);
        Assert.Equal("test-class", div.GetAttribute("class"));
        Assert.Equal("value", div.GetAttribute("data-from-props"));
        Assert.Equal(["data-from-props"], div.Attributes.Select(attribute => attribute.Name).Where(name => name.StartsWith("data-", StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "supports custom stateAttributesMapping for kebab-case conversion")]
    [Trait("Requirement", "RND-DOM-06")]
    public void Supports_custom_stateAttributesMapping_for_kebab_case_conversion()
    {
        StateDataAttribute[] isActive = [new("data-is-active", "")];
        StateAttributesMapping<MappingState> mapping = new StateAttributesMapping<MappingState>()
            .Map<bool>(nameof(MappingState.IsActive), value => value ? isActive : null)
            .Map<int>(nameof(MappingState.ItemCount), value => [new StateDataAttribute("data-item-count", value.ToString())])
            .Map<string?>(nameof(MappingState.UserName), value => [new StateDataAttribute("data-user-name", value ?? "")]);
        IRenderedComponent<RenderElement<MappingState>> cut = context.Render<RenderElement<MappingState>>(p => p
            .Add(x => x.Render, Button())
            .Add(x => x.State, new MappingState(IsActive: true, ItemCount: 5, UserName: "John"))
            .Add(x => x.StateAttributesMapping, mapping));

        IElement button = Root(cut);
        Assert.Equal("", button.GetAttribute("data-is-active"));
        Assert.Equal("5", button.GetAttribute("data-item-count"));
        Assert.Equal("John", button.GetAttribute("data-user-name"));
    }

    [Fact(DisplayName = "enabled false renders nothing and the element is captured otherwise")]
    [Trait("Requirement", "RND-API-07")]
    public void Enabled_false_renders_nothing_and_the_element_is_captured_otherwise()
    {
        IRenderedComponent<RenderElement<EmptyState>> cut = context.Render<RenderElement<EmptyState>>(p => p.Add(x => x.Enabled, false));
        Assert.Equal(string.Empty, cut.Markup);
        Assert.Null(cut.Instance.Element);

        cut.Render(p => p.Add(x => x.Enabled, true).AddChildContent("content"));
        Assert.Equal("content", Root(cut).TextContent);
        Assert.NotNull(cut.Instance.Element);
    }

    [Fact(DisplayName = "a stateless element passes the shared empty state to its callback")]
    [Trait("Requirement", "RND-STA-02")]
    public void A_stateless_element_passes_the_shared_empty_state_to_its_callback()
    {
        EmptyState? received = null;
        context.Render<RenderElement<EmptyState>>(p => p
            .Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => builder =>
            {
                received = props.State;
                builder.OpenElement(0, "span");
                builder.AddMultipleAttributes(1, props.Attributes);
                builder.CloseElement();
            })));

        Assert.Same(EmptyState.Instance, received);
    }

    [Fact(DisplayName = "the stateless form compiles and renders in markup")]
    [Trait("Requirement", "RND-STA-02")]
    public void The_stateless_form_compiles_and_renders_in_markup()
    {
        IRenderedComponent<Consumers.StatelessElementConsumer> cut = context.Render<Consumers.StatelessElementConsumer>();

        IElement element = cut.Find("span");
        Assert.DoesNotContain(element.Attributes, attribute => attribute.Name.StartsWith("data-", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "an element whose state type has fields requires a State")]
    [Trait("Requirement", "RND-STA-02")]
    public void An_element_whose_state_type_has_fields_requires_a_State()
    {
        InvalidOperationException missing = Assert.Throws<InvalidOperationException>(() => context.Render<RenderElement<ActiveIndexState>>());
        Assert.StartsWith("Blazix: ", missing.Message, StringComparison.Ordinal);
        Assert.Contains("`State`", missing.Message, StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(() => context.Render<RenderElement<ActiveIndexState>>(p => p.Add(x => x.State, null!)));
    }

    private static IElement Root<TState>(IRenderedComponent<RenderElement<TState>> cut)
        where TState : class, IPartState<TState>
        => cut.Nodes.OfType<IElement>().Single();

    private static RenderFragment Button() => builder =>
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "type", "button");
        builder.CloseElement();
    };

    private static RenderFragment Span() => builder =>
    {
        builder.OpenElement(0, "span");
        builder.CloseElement();
    };
}

[PartState]
public sealed partial record ActiveIndexState(bool Active, int Index);

[PartState]
public sealed partial record FormState(string? Form);

[PartState]
public sealed partial record MappingState(bool IsActive, int ItemCount, string? UserName);
