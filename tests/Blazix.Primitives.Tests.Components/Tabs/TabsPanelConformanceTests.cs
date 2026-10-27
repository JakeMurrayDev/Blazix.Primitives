using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TabsPanel.test.tsx: describeConformance(<Tabs.Panel value="1" keepMounted />, () => ({ render inside <Tabs.Root>,
// refInstanceof: window.HTMLDivElement })). TAB-STR-05.
/// <summary>The conformance suite on a kept-mounted <see cref="TabsPanel{TValue}"/> inside a <see cref="TabsRoot{TValue}"/>.</summary>
public sealed class TabsPanelConformanceTests : PartConformanceTests<TabsPanel<string>>
{
    public TabsPanelConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<TabsRoot<string>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<TabsPanel<string>> Options { get; } = new("div")
    {
        Required = p => p.Add(x => x.Value, "1").Add(x => x.KeepMounted, true),
    };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(TabsPanel<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<TabsPanel<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<TabsPanelState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<TabsPanel<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<TabsPanelState, string?>)(_ => classValue()));
    }
}
