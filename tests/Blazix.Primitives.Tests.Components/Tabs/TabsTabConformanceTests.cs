using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TabsTab.test.tsx: describeConformance(<Tabs.Tab value="1" />, () => ({ refInstanceof: window.HTMLButtonElement,
// testRenderPropWith: 'button', button: true, render inside <Tabs.Root><Tabs.List> })). TAB-STR-03, TAB-API-09 (the
// non-native re-run).
/// <summary>The conformance suite on <see cref="TabsTab{TValue}"/> inside a <see cref="TabsRoot{TValue}"/> and a <see cref="TabsList"/>.</summary>
public sealed class TabsTabConformanceTests : PartConformanceTests<TabsTab<string>>
{
    public TabsTabConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<TabsRoot<string>>();
        Context.RenderTree.Add<TabsList>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<TabsTab<string>> Options { get; } = new("button")
    {
        Button = true,
        TestRenderPropWith = "button",
        Required = p => p.Add(x => x.Value, "1"),
    };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(TabsTab<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<TabsTab<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<TabsTabState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<TabsTab<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<TabsTabState, string?>)(_ => classValue()));
    }
}
