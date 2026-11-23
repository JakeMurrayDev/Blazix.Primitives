using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ToolbarLink.test.tsx: describeConformance(<Toolbar.Link />, () => ({ refInstanceof: window.HTMLAnchorElement,
// testRenderPropWith: 'a', render: inside provided contexts })). TLB-DOM-12, TLB-API-11. The port renders the link inside
// a real ToolbarRoot (incidental under ADR 13 Amendments 2026-10-22).
/// <summary>The conformance suite on <see cref="ToolbarLink"/> inside a <see cref="ToolbarRoot"/>.</summary>
public sealed class ToolbarLinkConformanceTests : PartConformanceTests<ToolbarLink>
{
    public ToolbarLinkConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ToolbarRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToolbarLink> Options { get; } = new("a") { TestRenderPropWith = "a" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToolbarLink part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToolbarLink> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToolbarLinkState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToolbarLink> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToolbarLinkState, string?>)(_ => classValue()));
    }
}
