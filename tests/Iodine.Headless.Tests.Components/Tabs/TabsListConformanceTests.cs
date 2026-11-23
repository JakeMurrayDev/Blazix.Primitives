using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TabsList.test.tsx: describeConformance(<Tabs.List />, () => ({ render inside <Tabs.Root>, refInstanceof: window.HTMLDivElement })).
// TAB-STR-02.
/// <summary>The conformance suite on <see cref="TabsList"/> inside a <see cref="TabsRoot{TValue}"/>.</summary>
public sealed class TabsListConformanceTests : PartConformanceTests<TabsList>
{
    public TabsListConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<TabsRoot<int>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<TabsList> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(TabsList part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<TabsList> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<TabsListState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<TabsList> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<TabsListState, string?>)(_ => classValue()));
    }
}
