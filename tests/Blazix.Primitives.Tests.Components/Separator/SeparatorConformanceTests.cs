using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root, not a `Separator` namespace: a namespace named like the part would shadow the type
// `Blazix.Primitives.Separator` in every file declared inside it.
namespace Blazix.Primitives.Tests.Components;

// Separator.test.tsx: describeConformance(<Separator />, () => ({ render, refInstanceof: window.HTMLDivElement })).
// SEP-DOM-03 (RND-DOM-36..48; RND-DOM-49 does not apply, the part has no NativeButton), SEP-API-02, SEP-API-03.
/// <summary>The conformance suite on <see cref="Separator"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class SeparatorConformanceTests : PartConformanceTests<Separator>
{
    /// <inheritdoc />
    protected override ConformanceOptions<Separator> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(Separator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<Separator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SeparatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<Separator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SeparatorState, string?>)(_ => classValue()));
    }
}
