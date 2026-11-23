using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// MeterTrack.test.tsx: describeConformance(<Meter.Track />, () => ({ render: inside <Meter.Root value={30}>, refInstanceof: window.HTMLDivElement })). MTR-DOM-09, MTR-STR-04, MTR-API-08, MTR-API-13.
// RND-DOM-49 does not apply (no NativeButton).
/// <summary>The conformance suite on <see cref="MeterTrack"/> inside a <see cref="MeterRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class MeterTrackConformanceTests : PartConformanceTests<MeterTrack>
{
    public MeterTrackConformanceTests()
    {
        Context.RenderTree.Add<MeterRoot>(p => p.Add(x => x.Value, 30));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MeterTrack> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MeterTrack part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MeterTrack> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MeterTrack> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}
