using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ProgressValue.test.tsx: describeConformance(<Progress.Value />, () => ({ render: inside <Progress.Root value={40}>, refInstanceof: window.HTMLSpanElement })). PRG-DOM-10, PRG-API-08, PRG-API-13.
// RND-DOM-49 does not apply (no NativeButton).
/// <summary>The conformance suite on <see cref="ProgressValue"/> inside a <see cref="ProgressRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class ProgressValueConformanceTests : PartConformanceTests<ProgressValue>
{
    public ProgressValueConformanceTests()
    {
        Context.RenderTree.Add<ProgressRoot>(p => p.Add(x => x.Value, 40));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ProgressValue> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ProgressValue part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ProgressValue> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ProgressValueState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ProgressValue> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ProgressValueState, string?>)(_ => classValue()));
    }
}
