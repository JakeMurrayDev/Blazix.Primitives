using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// FieldControl.test.tsx: describeConformance(<Field.Control />, () => ({ refInstanceof: window.HTMLInputElement, render: inside <Field.Root> })). FLD-DOM-19, FLD-API-22/23.
/// <summary>The conformance suite on <see cref="FieldControl"/> inside a <see cref="FieldRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class FieldControlConformanceTests : PartConformanceTests<FieldControl>
{
    public FieldControlConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<FieldRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<FieldControl> Options { get; } = new("input");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(FieldControl part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<FieldControl> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<FieldControlState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<FieldControl> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<FieldControlState, string?>)(_ => classValue()));
    }
}
