using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// FieldItem.test.tsx: describeConformance(<Field.Item />, () => ({ refInstanceof: window.HTMLDivElement, render: inside <Field.Root> })). FLD-DOM-19, FLD-STR-06, FLD-API-28/29.
/// <summary>The conformance suite on <see cref="FieldItem"/> inside a <see cref="FieldRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class FieldItemConformanceTests : PartConformanceTests<FieldItem>
{
    public FieldItemConformanceTests()
    {
        Context.RenderTree.Add<FieldRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<FieldItem> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(FieldItem part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<FieldItem> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<FieldItemState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<FieldItem> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<FieldItemState, string?>)(_ => classValue()));
    }
}
