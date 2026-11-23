using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// CheckboxRoot.test.tsx: describeConformance(<Checkbox.Root />, () => ({ refInstanceof: window.HTMLSpanElement,
// testRenderPropWith: 'span', button: true })). CHK-DOM-20, CHK-API-16. With testRenderPropWith a span, every substitution
// passes NativeButton="false" and RND-DOM-49 does not run.
/// <summary>The conformance suite on <see cref="CheckboxRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class CheckboxRootConformanceTests : PartConformanceTests<CheckboxRoot>
{
    public CheckboxRootConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; CheckboxRootTests records them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<CheckboxRoot> Options { get; } = new("span") { Button = true, TestRenderPropWith = "span" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(CheckboxRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<CheckboxRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<CheckboxRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<CheckboxRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<CheckboxRootState, string?>)(_ => classValue()));
    }
}
