using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ProgressRoot.test.tsx: describeConformance(<Progress.Root value={50} />, () => ({ render, refInstanceof: window.HTMLDivElement })).
// PRG-DOM-10, PRG-API-08, PRG-API-09; `Value` is the one required parameter. RND-DOM-49 does not apply (no NativeButton).
/// <summary>The conformance suite on <see cref="ProgressRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class ProgressRootConformanceTests : PartConformanceTests<ProgressRoot>
{
    /// <inheritdoc />
    protected override ConformanceOptions<ProgressRoot> Options { get; } = new("div")
    {
        Required = p => p.Add(x => x.Value, 50),
    };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ProgressRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ProgressRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ProgressRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ProgressRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ProgressRootState, string?>)(_ => classValue()));
    }
}
