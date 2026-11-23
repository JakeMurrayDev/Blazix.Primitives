using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// MeterRoot.test.tsx: describeConformance(<Meter.Root value={50} />, () => ({ render, refInstanceof: window.HTMLDivElement })).
// MTR-DOM-09, MTR-API-08, MTR-API-09; `Value` is the one required parameter. RND-DOM-49 does not apply (no NativeButton).
/// <summary>The conformance suite on <see cref="MeterRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class MeterRootConformanceTests : PartConformanceTests<MeterRoot>
{
    /// <inheritdoc />
    protected override ConformanceOptions<MeterRoot> Options { get; } = new("div")
    {
        Required = p => p.Add(x => x.Value, 50),
    };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MeterRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MeterRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MeterRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}
