using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the part's type.
namespace Blazix.Primitives.Tests.Components;

// ToggleGroup.test.tsx: describeConformance(<ToggleGroup />, () => ({ refInstanceof: window.HTMLDivElement })).
// TGG-DOM-08, TGG-API-08, TGG-API-09, TGG-DOM-01, TGG-DOM-07. No `button` option, so RND-DOM-49 does not run.
/// <summary>The conformance suite on <see cref="ToggleGroup"/>: the shared RND-DOM-36..48 tests with Base UI's titles.</summary>
public sealed class ToggleGroupConformanceTests : PartConformanceTests<ToggleGroup>
{
    public ToggleGroupConformanceTests()
    {
        // The composite twin's import and attach are not the subject here; ToggleGroupTests records them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToggleGroup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToggleGroup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToggleGroup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToggleGroupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToggleGroup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToggleGroupState, string?>)(_ => classValue()));
    }
}
