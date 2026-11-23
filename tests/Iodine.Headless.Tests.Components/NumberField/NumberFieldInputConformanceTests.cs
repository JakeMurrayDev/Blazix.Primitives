using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// NumberFieldInput.test.tsx: describeConformance(<NumberField.Input />, () => ({ refInstanceof: window.HTMLInputElement, render: inside a Root })).
// NUM-DOM-16, NUM-API-20, NUM-API-21, NUM-API-25.
/// <summary>The conformance suite on <see cref="NumberFieldInput"/> inside a <see cref="NumberFieldRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class NumberFieldInputConformanceTests : PartConformanceTests<NumberFieldInput>
{
    public NumberFieldInputConformanceTests()
    {
        // The twins' imports and attaches are not the subject here.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NumberFieldRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NumberFieldInput> Options { get; } = new("input");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NumberFieldInput part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NumberFieldInput> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NumberFieldInputState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NumberFieldInput> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NumberFieldInputState, string?>)(_ => classValue()));
    }
}
