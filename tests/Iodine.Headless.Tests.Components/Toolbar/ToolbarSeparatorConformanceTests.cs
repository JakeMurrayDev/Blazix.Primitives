using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ToolbarSeparator.test.tsx: describeConformance(<Toolbar.Separator />, () => ({ refInstanceof: window.HTMLDivElement,
// render: inside <Toolbar.Root> })). TLB-DOM-12, TLB-API-18.
/// <summary>The conformance suite on <see cref="ToolbarSeparator"/> inside a <see cref="ToolbarRoot"/>.</summary>
public sealed class ToolbarSeparatorConformanceTests : PartConformanceTests<ToolbarSeparator>
{
    public ToolbarSeparatorConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ToolbarRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToolbarSeparator> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToolbarSeparator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToolbarSeparator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToolbarSeparatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToolbarSeparator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToolbarSeparatorState, string?>)(_ => classValue()));
    }
}
