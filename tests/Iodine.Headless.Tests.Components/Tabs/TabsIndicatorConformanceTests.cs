using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TabsIndicator.test.tsx: describeConformance(<Tabs.Indicator />, () => ({ render inside <Tabs.Root defaultValue={1}>
// <Tabs.List> beside a Tab, refInstanceof: window.HTMLSpanElement, testRenderPropWith: 'div' })). TAB-STR-04, TAB-DOM-32
// (the style forwarding; the precedence of the JS-written variables is L4).
/// <summary>The conformance suite on <see cref="TabsIndicator"/> inside a <see cref="TabsRoot{TValue}"/> with a value and a <see cref="TabsList"/>.</summary>
public sealed class TabsIndicatorConformanceTests : PartConformanceTests<TabsIndicator>
{
    public TabsIndicatorConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<TabsRoot<int>>(p => p.Add(x => x.DefaultValue, 1));
        Context.RenderTree.Add<TabsList>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<TabsIndicator> Options { get; } = new("span")
    {
        TestRenderPropWith = "div",
    };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(TabsIndicator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<TabsIndicator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<TabsIndicatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<TabsIndicator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<TabsIndicatorState, string?>)(_ => classValue()));
    }
}
