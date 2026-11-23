using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// NumberFieldRoot.test.tsx: describeConformance(<NumberField.Root />, () => ({ refInstanceof: window.HTMLDivElement })).
// NUM-DOM-16, NUM-API-20, NUM-API-21, NUM-API-25.
/// <summary>The conformance suite on <see cref="NumberFieldRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class NumberFieldRootConformanceTests : PartConformanceTests<NumberFieldRoot>
{
    public NumberFieldRootConformanceTests()
    {
        // The twins' imports and attaches are not the subject here.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NumberFieldRoot> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NumberFieldRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NumberFieldRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NumberFieldRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NumberFieldRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NumberFieldRootState, string?>)(_ => classValue()));
    }
}
