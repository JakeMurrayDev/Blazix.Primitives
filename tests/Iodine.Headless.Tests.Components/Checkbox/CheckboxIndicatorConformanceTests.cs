using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// CheckboxIndicator.test.tsx: describeConformance(<Checkbox.Indicator />, () => ({ refInstanceof: window.HTMLSpanElement, render: inside
// a provided root state })). CHK-DOM-20, CHK-API-19. The port renders the indicator inside a checked CheckboxRoot, which mounts it.
/// <summary>The conformance suite on <see cref="CheckboxIndicator"/> inside a checked <see cref="CheckboxRoot"/>.</summary>
public sealed class CheckboxIndicatorConformanceTests : PartConformanceTests<CheckboxIndicator>
{
    public CheckboxIndicatorConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<CheckboxRoot>(p => p.Add(x => x.Checked, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<CheckboxIndicator> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(CheckboxIndicator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<CheckboxIndicator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<CheckboxIndicatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<CheckboxIndicator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<CheckboxIndicatorState, string?>)(_ => classValue()));
    }
}
