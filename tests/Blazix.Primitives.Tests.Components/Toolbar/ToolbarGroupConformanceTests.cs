using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ToolbarGroup.test.tsx: describeConformance(<Toolbar.Group />, () => ({ refInstanceof: window.HTMLDivElement, render:
// inside provided toolbar and composite contexts })). TLB-DOM-12, TLB-API-16. The port renders the group inside a real
// ToolbarRoot (incidental under ADR 13 Amendments 2026-10-22; the SwitchThumb precedent).
/// <summary>The conformance suite on <see cref="ToolbarGroup"/> inside a <see cref="ToolbarRoot"/>.</summary>
public sealed class ToolbarGroupConformanceTests : PartConformanceTests<ToolbarGroup>
{
    public ToolbarGroupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ToolbarRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToolbarGroup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToolbarGroup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToolbarGroup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToolbarGroupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToolbarGroup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToolbarGroupState, string?>)(_ => classValue()));
    }
}
