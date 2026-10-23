using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// RadioRoot.test.tsx: describeConformance(<Radio.Root value="" />, () => ({ refInstanceof: window.HTMLSpanElement,
// testRenderPropWith: 'span', button: true })). RAD-DOM-17, RAD-API-08. With testRenderPropWith a span, every substitution
// passes NativeButton="false" and RND-DOM-49 does not run; the required Value is the empty string, as Base UI's suite.
/// <summary>The conformance suite on <see cref="RadioRoot{TValue}"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class RadioRootConformanceTests : PartConformanceTests<RadioRoot<string>>
{
    public RadioRootConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; RadioRootTests records them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<RadioRoot<string>> Options { get; } = new("span")
    {
        Button = true,
        TestRenderPropWith = "span",
        Required = p => p.Add(x => x.Value, string.Empty),
    };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(RadioRoot<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<RadioRoot<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<RadioRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<RadioRoot<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<RadioRootState, string?>)(_ => classValue()));
    }
}
