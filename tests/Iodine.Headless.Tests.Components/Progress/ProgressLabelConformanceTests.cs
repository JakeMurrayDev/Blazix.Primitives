using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ProgressLabel.test.tsx: describeConformance(<Progress.Label />, () => ({ render: inside <Progress.Root value={40}>, refInstanceof: window.HTMLSpanElement })). PRG-DOM-10, PRG-API-08, PRG-API-13.
// RND-DOM-49 does not apply (no NativeButton).
/// <summary>The conformance suite on <see cref="ProgressLabel"/> inside a <see cref="ProgressRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class ProgressLabelConformanceTests : PartConformanceTests<ProgressLabel>
{
    public ProgressLabelConformanceTests()
    {
        // The label publishes its id to the root, which renders again; bUnit drops the element-reference marker of an element
        // that renders again, which RND-DOM-44/45 look for. A root with its own aria-labelledby ignores the published id and
        // renders nothing more (PRG-ARIA-06), so the suite sees the first render of the label.
        Context.RenderTree.Add<ProgressRoot>(p => p.Add(x => x.Value, 40).AddUnmatched("aria-labelledby", "conformance"));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ProgressLabel> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ProgressLabel part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ProgressLabel> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ProgressLabelState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ProgressLabel> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ProgressLabelState, string?>)(_ => classValue()));
    }
}
