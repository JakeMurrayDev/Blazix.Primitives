using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SwitchThumb.test.tsx: describeConformance(<Switch.Thumb />, () => ({ refInstanceof: window.HTMLSpanElement, render: inside
// a provided root state })). SWT-DOM-17, SWT-API-16. The port renders the thumb inside a SwitchRoot.
/// <summary>The conformance suite on <see cref="SwitchThumb"/> inside a <see cref="SwitchRoot"/>.</summary>
public sealed class SwitchThumbConformanceTests : PartConformanceTests<SwitchThumb>
{
    public SwitchThumbConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SwitchRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SwitchThumb> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SwitchThumb part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SwitchThumb> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SwitchThumbState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SwitchThumb> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SwitchThumbState, string?>)(_ => classValue()));
    }
}
