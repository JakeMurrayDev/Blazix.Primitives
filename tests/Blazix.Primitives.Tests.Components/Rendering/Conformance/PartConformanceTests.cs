using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Rendering.Conformance;

/// <summary>
/// The conformance suite every part runs (RND-DOM-36..49; <c>test/describeConformance.tsx</c> and
/// <c>test/conformanceTests/*.tsx</c>), with Base UI's titles. A family's test class derives from it, supplies the
/// <see cref="Options"/>, and inherits every test. Relaxations (ADR 02): the consumer handle is the part's
/// <c>Element</c>, which is <c>null</c> under a substitute callback and under a component root, so RND-DOM-44 asserts
/// the capture the callback itself makes and RND-DOM-45 runs on the element form; RND-DOM-41 asserts the tag.
/// </summary>
public abstract class PartConformanceTests<TPart> : IDisposable
    where TPart : class, IComponent
{
    private static int s_counter;

    protected PartConformanceTests()
    {
        Context = new BunitContext();
        Context.Services.AddBlazixPrimitives();
    }

    protected BunitContext Context { get; }

    protected abstract ConformanceOptions<TPart> Options { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "forwards custom props to the default element")]
    [Trait("Requirement", "RND-DOM-36")]
    public void Forwards_custom_props_to_the_default_element()
    {
        string foobar = RandomStringValue();

        IRenderedComponent<TPart> cut = Render(p => p
            .AddUnmatched("data-testid", "root")
            .AddUnmatched("lang", "fr")
            .AddUnmatched("data-foobar", foobar));

        IElement customRoot = cut.Find("[data-testid=root]");
        Assert.Equal("fr", customRoot.GetAttribute("lang"));
        Assert.Equal(foobar, customRoot.GetAttribute("data-foobar"));
    }

    [Fact(DisplayName = "forwards custom props to the customized element defined with a function")]
    [Trait("Requirement", "RND-DOM-37")]
    public void Forwards_custom_props_to_the_customized_element_defined_with_a_function()
    {
        string foobar = RandomStringValue();

        IRenderedComponent<TPart> cut = Render(p =>
        {
            p.AddUnmatched("lang", "fr").AddUnmatched("data-foobar", foobar);
            AddNativeButton(p);
            SetRenderFunction(p, attributes => builder =>
            {
                builder.OpenElement(0, Options.TestRenderPropWith);
                builder.AddMultipleAttributes(1, attributes);
                builder.AddAttribute(2, "data-testid", "custom-root");
                builder.CloseElement();
            });
        });

        IElement customRoot = cut.Find("[data-testid=custom-root]");
        Assert.Equal("fr", customRoot.GetAttribute("lang"));
        Assert.Equal(foobar, customRoot.GetAttribute("data-foobar"));
    }

    [Fact(DisplayName = "forwards custom props to the customized element defined using JSX")]
    [Trait("Requirement", "RND-DOM-38")]
    public void Forwards_custom_props_to_the_customized_element_defined_using_JSX()
    {
        string foobar = RandomStringValue();

        IRenderedComponent<TPart> cut = Render(p =>
        {
            p.AddUnmatched("lang", "fr").AddUnmatched("data-foobar", foobar);
            AddNativeButton(p);
            SetRender(p, builder =>
            {
                builder.OpenElement(0, Options.TestRenderPropWith);
                builder.AddAttribute(1, "data-testid", "custom-root");
                builder.CloseElement();
            });
        });

        IElement customRoot = cut.Find("[data-testid=custom-root]");
        Assert.Equal("fr", customRoot.GetAttribute("lang"));
        Assert.Equal(foobar, customRoot.GetAttribute("data-foobar"));
    }

    [Fact(DisplayName = "forwards the custom `style` attribute defined on the component")]
    [Trait("Requirement", "RND-DOM-39")]
    public void Forwards_the_custom_style_attribute_defined_on_the_component()
    {
        IRenderedComponent<TPart> cut = Render(p => p
            .AddUnmatched("style", "color: green")
            .AddUnmatched("data-testid", "custom-root"));

        IElement customRoot = cut.Find("[data-testid=custom-root]");
        Assert.True(customRoot.HasAttribute("style"));
        Assert.Contains("color: green", customRoot.GetAttribute("style"));
    }

    [Fact(DisplayName = "forwards the custom `style` attribute defined on the render element")]
    [Trait("Requirement", "RND-DOM-40")]
    public void Forwards_the_custom_style_attribute_defined_on_the_render_element()
    {
        IRenderedComponent<TPart> cut = Render(p =>
        {
            AddNativeButton(p);
            SetRender(p, builder =>
            {
                builder.OpenElement(0, Options.TestRenderPropWith);
                builder.AddAttribute(1, "style", "color: green");
                builder.AddAttribute(2, "data-testid", "custom-root");
                builder.CloseElement();
            });
        });

        IElement customRoot = cut.Find("[data-testid=custom-root]");
        Assert.True(customRoot.HasAttribute("style"));
        Assert.Contains("color: green", customRoot.GetAttribute("style"));
    }

    [Fact(DisplayName = "attaches the ref")]
    [Trait("Requirement", "RND-DOM-41")]
    public void Attaches_the_ref()
    {
        IRenderedComponent<TPart> cut = Render(_ => { });

        ElementReference? element = ElementOf(cut.Instance);
        Assert.NotNull(element);
        Assert.Equal(Options.TagName, FindCaptured(cut, element.Value).TagName, ignoreCase: true);
    }

    [Fact(DisplayName = "renders a customized root element with a function")]
    [Trait("Requirement", "RND-DOM-42")]
    public void Renders_a_customized_root_element_with_a_function()
    {
        string testValue = RandomStringValue();

        IRenderedComponent<TPart> cut = Render(p =>
        {
            AddNativeButton(p);
            SetRenderFunction(p, attributes => builder =>
            {
                builder.OpenComponent<ConformanceWrapper>(0);
                builder.AddComponentParameter(1, nameof(ConformanceWrapper.Tag), Options.TestRenderPropWith);
                builder.AddComponentParameter(2, nameof(ConformanceWrapper.WrappingAllowed), Options.WrappingAllowed);
                builder.AddMultipleAttributes(3, attributes);
                builder.AddComponentParameter(4, "data-test-value", testValue);
                builder.CloseComponent();
            });
        });

        Assert.Equal(Options.WrappingAllowed ? 1 : 0, cut.FindAll("[data-testid=base-ui-wrapper]").Count);
        IElement wrapped = cut.Find("[data-testid=wrapped]");
        Assert.Equal(testValue, wrapped.GetAttribute("data-test-value"));
    }

    [Fact(DisplayName = "renders a customized root element with an element")]
    [Trait("Requirement", "RND-DOM-43")]
    public void Renders_a_customized_root_element_with_an_element()
    {
        string testValue = RandomStringValue();

        IRenderedComponent<TPart> cut = Render(p =>
        {
            AddNativeButton(p);
            SetRender(p, builder =>
            {
                builder.OpenComponent<ConformanceWrapper>(0);
                builder.AddComponentParameter(1, nameof(ConformanceWrapper.Tag), Options.TestRenderPropWith);
                builder.AddComponentParameter(2, nameof(ConformanceWrapper.WrappingAllowed), Options.WrappingAllowed);
                builder.AddComponentParameter(3, "data-test-value", testValue);
                builder.CloseComponent();
            });
        });

        Assert.Equal(Options.WrappingAllowed ? 1 : 0, cut.FindAll("[data-testid=base-ui-wrapper]").Count);
        IElement wrapped = cut.Find("[data-testid=wrapped]");
        Assert.Equal(testValue, wrapped.GetAttribute("data-test-value"));
    }

    [Fact(DisplayName = "should pass the ref to the custom component")]
    [Trait("Requirement", "RND-DOM-44")]
    public void Should_pass_the_ref_to_the_custom_component()
    {
        // ADR 02 relaxation: under a substitute callback the part's Element is null and the callback's own capture is
        // the consumer handle; it receives the element the callback rendered, with the attributes the part forwarded.
        ElementReference? instanceFromRef = null;

        IRenderedComponent<TPart> cut = Render(p =>
        {
            p.AddUnmatched("data-testid", "wrapped");
            AddNativeButton(p);
            SetRenderFunction(p, attributes => builder =>
            {
                builder.OpenElement(0, Options.TestRenderPropWith);
                builder.AddMultipleAttributes(1, attributes);
                builder.AddElementReferenceCapture(2, element => instanceFromRef = element);
                builder.CloseElement();
            });
        });

        Assert.Null(ElementOf(cut.Instance));
        Assert.NotNull(instanceFromRef);
        IElement captured = FindCaptured(cut, instanceFromRef.Value);
        Assert.Equal(Options.TestRenderPropWith, captured.TagName, ignoreCase: true);
        Assert.Equal("wrapped", captured.GetAttribute("data-testid"));
    }

    [Fact(DisplayName = "should merge the rendering element ref with the custom component ref")]
    [Trait("Requirement", "RND-DOM-45")]
    public void Should_merge_the_rendering_element_ref_with_the_custom_component_ref()
    {
        ElementReference? refB = null;

        IRenderedComponent<TPart> cut = Render(p =>
        {
            p.AddUnmatched("data-testid", "wrapped");
            AddNativeButton(p);
            SetRender(p, builder =>
            {
                builder.OpenElement(0, Options.TestRenderPropWith);
                builder.AddElementReferenceCapture(1, element => refB = element);
                builder.CloseElement();
            });
        });

        ElementReference? refA = ElementOf(cut.Instance);
        Assert.NotNull(refA);
        IElement a = FindCaptured(cut, refA.Value);
        Assert.Equal(Options.TestRenderPropWith, a.TagName, ignoreCase: true);
        Assert.Equal("wrapped", a.GetAttribute("data-testid"));
        // ADR 06 §1: non-null capture for RND-DOM-45. bUnit's markup carries only the first capture of an element
        // (the part's own), so the substitute's capture is asserted to have been made; the browser suite checks identity.
        Assert.NotNull(refB);
        Assert.False(string.IsNullOrEmpty(refB.Value.Id));
    }

    [Fact(DisplayName = "should merge the rendering element className with the custom component className")]
    [Trait("Requirement", "RND-DOM-46")]
    public void Should_merge_the_rendering_element_className_with_the_custom_component_className()
    {
        IRenderedComponent<TPart> cut = Render(p =>
        {
            p.AddUnmatched("class", "component-classname").AddUnmatched("data-testid", "test-component");
            AddNativeButton(p);
            SetRender(p, builder =>
            {
                builder.OpenElement(0, Options.TestRenderPropWith);
                builder.AddAttribute(1, "class", "render-prop-classname");
                builder.CloseElement();
            });
        });

        IElement component = cut.Find("[data-testid=test-component]");
        Assert.True(component.ClassList.Contains("component-classname"));
        Assert.True(component.ClassList.Contains("render-prop-classname"));
    }

    [Fact(DisplayName = "should merge the rendering element resolved className with the custom component className")]
    [Trait("Requirement", "RND-DOM-47")]
    public void Should_merge_the_rendering_element_resolved_className_with_the_custom_component_className()
    {
        IRenderedComponent<TPart> cut = Render(p =>
        {
            p.AddUnmatched("data-testid", "test-component");
            AddNativeButton(p);
            SetClassValue(p, () => "conditional-component-classname");
            SetRender(p, builder =>
            {
                builder.OpenElement(0, Options.TestRenderPropWith);
                builder.AddAttribute(1, "class", "render-prop-classname");
                builder.CloseElement();
            });
        });

        IElement component = cut.Find("[data-testid=test-component]");
        Assert.True(component.ClassList.Contains("conditional-component-classname"));
        Assert.True(component.ClassList.Contains("render-prop-classname"));
    }

    [Fact(DisplayName = "should apply the className to the root element when passed as a string")]
    [Trait("Requirement", "RND-DOM-48")]
    public void Should_apply_the_className_to_the_root_element_when_passed_as_a_string()
    {
        IRenderedComponent<TPart> cut = Render(p => p.AddUnmatched("class", "test-class").AddUnmatched("data-testid", "root"));

        Assert.True(cut.Find("[data-testid=root]").ClassList.Contains("test-class"));
    }

    [Fact(DisplayName = "with a non-native button: forwards custom props to the customized element defined using JSX")]
    [Trait("Requirement", "RND-DOM-49")]
    public void With_a_non_native_button_forwards_custom_props_to_the_customized_element_defined_using_JSX()
    {
        Assert.SkipUnless(Options.Button && Options.TestRenderPropWith == "button", "the part is not a button part substituted with a button");
        string foobar = RandomStringValue();

        IRenderedComponent<TPart> cut = Render(p =>
        {
            p.AddUnmatched("lang", "fr").AddUnmatched("data-foobar", foobar).AddUnmatched("NativeButton", false);
            SetRender(p, builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "data-testid", "custom-root");
                builder.CloseElement();
            });
        });

        IElement customRoot = cut.Find("[data-testid=custom-root]");
        Assert.Equal("DIV", customRoot.TagName, ignoreCase: true);
        Assert.Equal("fr", customRoot.GetAttribute("lang"));
        Assert.Equal(foobar, customRoot.GetAttribute("data-foobar"));
    }

    [Fact(DisplayName = "with a non-native button: renders a customized root element with an element")]
    [Trait("Requirement", "RND-DOM-49")]
    public void With_a_non_native_button_renders_a_customized_root_element_with_an_element()
    {
        Assert.SkipUnless(Options.Button && Options.TestRenderPropWith == "button", "the part is not a button part substituted with a button");
        string testValue = RandomStringValue();

        IRenderedComponent<TPart> cut = Render(p =>
        {
            p.AddUnmatched("NativeButton", false);
            SetRender(p, builder =>
            {
                builder.OpenComponent<ConformanceWrapper>(0);
                builder.AddComponentParameter(1, nameof(ConformanceWrapper.Tag), "div");
                builder.AddComponentParameter(2, nameof(ConformanceWrapper.WrappingAllowed), Options.WrappingAllowed);
                builder.AddComponentParameter(3, "data-test-value", testValue);
                builder.CloseComponent();
            });
        });

        Assert.Equal(testValue, cut.Find("[data-testid=wrapped]").GetAttribute("data-test-value"));
    }

    /// <summary>Renders the minimal element with the suite's parameters on top of <see cref="ConformanceOptions{TPart}.Required"/>.</summary>
    protected IRenderedComponent<TPart> Render(Action<ComponentParameterCollectionBuilder<TPart>> parameters)
    {
        return Context.Render<TPart>(p =>
        {
            Options.Required?.Invoke(p);
            parameters(p);
        });
    }

    /// <summary>The part's consumer handle (ADR 02: <c>Element</c>).</summary>
    protected abstract ElementReference? ElementOf(TPart part);

    /// <summary>Sets the substitute element parameter; a family with another parameter shape overrides.</summary>
    protected virtual void SetRender(ComponentParameterCollectionBuilder<TPart> p, RenderFragment fragment) => p.AddUnmatched("Render", fragment);

    /// <summary>Sets the substitute callback parameter, given the merged attributes as the dictionary the callback splats.</summary>
    protected abstract void SetRenderFunction(ComponentParameterCollectionBuilder<TPart> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render);

    /// <summary>Sets the class function parameter.</summary>
    protected abstract void SetClassValue(ComponentParameterCollectionBuilder<TPart> p, Func<string?> classValue);

    /// <summary>Finds the rendered element a capture refers to: bUnit writes <c>blazor:elementReference</c> on the element, which AngleSharp lowercases.</summary>
    protected static IElement FindCaptured(IRenderedComponent<TPart> cut, ElementReference element)
    {
        return cut.Find($"[blazor\\:elementreference='{element.Id}']");
    }

    private void AddNativeButton(ComponentParameterCollectionBuilder<TPart> p)
    {
        if (Options.Button)
        {
            p.AddUnmatched("NativeButton", Options.TestRenderPropWith == "button");
        }
    }

    private static string RandomStringValue() => "value-" + Interlocked.Increment(ref s_counter);
}
