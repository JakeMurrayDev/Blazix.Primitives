using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ProgressTrack.test.tsx: describeConformance(<Progress.Track />, () => ({ render: inside <Progress.Root value={40}>, refInstanceof: window.HTMLDivElement })). PRG-DOM-10, PRG-API-08, PRG-API-13.
// RND-DOM-49 does not apply (no NativeButton).
/// <summary>The conformance suite on <see cref="ProgressTrack"/> inside a <see cref="ProgressRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class ProgressTrackConformanceTests : PartConformanceTests<ProgressTrack>
{
    public ProgressTrackConformanceTests()
    {
        Context.RenderTree.Add<ProgressRoot>(p => p.Add(x => x.Value, 40));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ProgressTrack> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ProgressTrack part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ProgressTrack> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ProgressTrackState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ProgressTrack> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ProgressTrackState, string?>)(_ => classValue()));
    }
}
