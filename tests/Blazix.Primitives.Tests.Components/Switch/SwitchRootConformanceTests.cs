using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SwitchRoot.test.tsx: describeConformance(<Switch.Root />, () => ({ refInstanceof: window.HTMLSpanElement,
// testRenderPropWith: 'span', button: true })). SWT-DOM-17, SWT-API-14. With testRenderPropWith a span, every substitution
// passes NativeButton="false" and RND-DOM-49 does not run.
/// <summary>The conformance suite on <see cref="SwitchRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class SwitchRootConformanceTests : PartConformanceTests<SwitchRoot>
{
    public SwitchRootConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; SwitchRootTests records them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SwitchRoot> Options { get; } = new("span") { Button = true, TestRenderPropWith = "span" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SwitchRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SwitchRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SwitchRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SwitchRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SwitchRootState, string?>)(_ => classValue()));
    }
}
