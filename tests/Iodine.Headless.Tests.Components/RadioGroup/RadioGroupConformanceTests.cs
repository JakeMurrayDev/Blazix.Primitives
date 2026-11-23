using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// RadioGroup.test.tsx: describeConformance(<RadioGroup />, () => ({ refInstanceof: window.HTMLDivElement })). RDG-DOM-07,
// RDG-API-11, RDG-STR-03 (a group with no radio renders its element).
/// <summary>The conformance suite on <see cref="RadioGroup{TValue}"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class RadioGroupConformanceTests : PartConformanceTests<RadioGroup<string>>
{
    public RadioGroupConformanceTests()
    {
        // The composite twin's import and attach are not the subject here; RadioGroupTests records them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<RadioGroup<string>> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(RadioGroup<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<RadioGroup<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<RadioGroupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<RadioGroup<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<RadioGroupState, string?>)(_ => classValue()));
    }
}
