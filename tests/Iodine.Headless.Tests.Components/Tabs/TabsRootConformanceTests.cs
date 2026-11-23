using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TabsRoot.test.tsx: describeConformance(<Tabs.Root value={0} />, () => ({ refInstanceof: window.HTMLDivElement })). TAB-STR-01.
/// <summary>The conformance suite on <see cref="TabsRoot{TValue}"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class TabsRootConformanceTests : PartConformanceTests<TabsRoot<int>>
{
    public TabsRootConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<TabsRoot<int>> Options { get; } = new("div")
    {
        Required = p => p.Add(x => x.Value, 0),
    };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(TabsRoot<int> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<TabsRoot<int>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<TabsRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<TabsRoot<int>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<TabsRootState, string?>)(_ => classValue()));
    }
}
