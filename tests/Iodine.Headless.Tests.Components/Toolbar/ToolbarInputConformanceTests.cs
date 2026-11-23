using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ToolbarInput.test.tsx: describeConformance(<Toolbar.Input />, () => ({ refInstanceof: window.HTMLInputElement,
// testRenderPropWith: 'input', render: inside provided contexts })). TLB-DOM-12, TLB-API-14. The port renders the input
// inside a real ToolbarRoot (incidental under ADR 13 Amendments 2026-10-22).
/// <summary>The conformance suite on <see cref="ToolbarInput"/> inside a <see cref="ToolbarRoot"/>.</summary>
public sealed class ToolbarInputConformanceTests : PartConformanceTests<ToolbarInput>
{
    public ToolbarInputConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ToolbarRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToolbarInput> Options { get; } = new("input") { TestRenderPropWith = "input" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToolbarInput part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToolbarInput> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToolbarInputState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToolbarInput> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToolbarInputState, string?>)(_ => classValue()));
    }
}
