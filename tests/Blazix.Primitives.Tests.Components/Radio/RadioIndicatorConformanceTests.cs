using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// RadioIndicator.test.tsx: describeConformance(<Radio.Indicator />, () => ({ refInstanceof: window.HTMLSpanElement, render:
// inside <Radio.Root value=""> })). RAD-DOM-17, RAD-API-11, RAD-STA-01: a standalone root whose value is the empty string is
// checked (radio §16 Q1, resolved as written), so the indicator mounts.
/// <summary>The conformance suite on <see cref="RadioIndicator"/> inside a checked standalone <see cref="RadioRoot{TValue}"/>.</summary>
public sealed class RadioIndicatorConformanceTests : PartConformanceTests<RadioIndicator>
{
    public RadioIndicatorConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<RadioRoot<string>>(p => p.Add(x => x.Value, string.Empty));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<RadioIndicator> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(RadioIndicator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<RadioIndicator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<RadioIndicatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<RadioIndicator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<RadioIndicatorState, string?>)(_ => classValue()));
    }
}
