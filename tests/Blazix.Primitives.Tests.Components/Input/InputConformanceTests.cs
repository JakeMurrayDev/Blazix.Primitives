using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// Input.test.tsx: describeConformance(<Input />, () => ({ refInstanceof: window.HTMLInputElement, render })), standalone. INP-DOM-05, INP-STR-01..03, INP-API-08/09.
/// <summary>The conformance suite on <see cref="Input"/>, standalone: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class InputConformanceTests : PartConformanceTests<Input>
{
    public InputConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<Input> Options { get; } = new("input");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(Input part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<Input> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<InputState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<Input> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<InputState, string?>)(_ => classValue()));
    }
}
