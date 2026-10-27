using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ToolbarButton.test.tsx: describeConformance(<Toolbar.Button />, () => ({ refInstanceof: window.HTMLButtonElement,
// testRenderPropWith: 'button', button: true, render: inside provided contexts })). TLB-DOM-12, TLB-API-08, TLB-API-10.
// The port renders the button inside a real ToolbarRoot (incidental under ADR 13 Amendments 2026-10-22); with
// testRenderPropWith a button, every substitution passes NativeButton="true" and RND-DOM-49's two titles re-run with a div
// and NativeButton="false".
/// <summary>The conformance suite on <see cref="ToolbarButton"/> inside a <see cref="ToolbarRoot"/>.</summary>
public sealed class ToolbarButtonConformanceTests : PartConformanceTests<ToolbarButton>
{
    public ToolbarButtonConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ToolbarRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToolbarButton> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToolbarButton part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToolbarButton> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToolbarButtonState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToolbarButton> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToolbarButtonState, string?>)(_ => classValue()));
    }
}
