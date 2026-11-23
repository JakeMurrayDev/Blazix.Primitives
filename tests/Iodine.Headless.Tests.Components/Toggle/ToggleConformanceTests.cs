using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace: a `Toggle` namespace would hide the `Toggle` type.
namespace Blazix.Primitives.Tests.Components;

// Toggle.test.tsx: describeConformance(<Toggle />, () => ({ refInstanceof: window.HTMLButtonElement,
// testRenderPropWith: 'button', button: true })). TGL-DOM-07, TGL-API-06, TGL-API-07, TGL-API-08, TGL-DOM-01, TGL-DOM-04,
// TGL-DOM-06. With testRenderPropWith a button, every substitution passes NativeButton="true" and RND-DOM-49's two
// titles re-run with a div and NativeButton="false".
/// <summary>The conformance suite on <see cref="Toggle"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class ToggleConformanceTests : PartConformanceTests<Toggle>
{
    public ToggleConformanceTests()
    {
        // The twin's import and attach are not the subject here; ToggleTests records them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<Toggle> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(Toggle part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<Toggle> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToggleState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<Toggle> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToggleState, string?>)(_ => classValue()));
    }
}
