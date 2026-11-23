using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the part's type.
namespace Blazix.Primitives.Tests.Components;

// Form.test.tsx: describeConformance(<Form />, () => ({ refInstanceof: window.HTMLFormElement, render })). FRM-DOM-04,
// FRM-API-07/08, FRM-STA-01, FRM-DOM-02, FRM-EDGE-06.
/// <summary>The conformance suite on <see cref="Form"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class FormConformanceTests : PartConformanceTests<Form>
{
    public FormConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<Form> Options { get; } = new("form");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(Form part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<Form> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<Form> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}
