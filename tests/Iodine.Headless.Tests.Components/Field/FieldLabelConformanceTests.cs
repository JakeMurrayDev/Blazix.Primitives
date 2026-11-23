using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// FieldLabel.test.tsx: describeConformance(<Field.Label />, () => ({ refInstanceof: window.HTMLLabelElement, testRenderPropWith: 'label', render: inside <Field.Root> })). FLD-DOM-19, FLD-API-13/14. Meter hand-over fact 1: the label publishes its id and the field renders again, which would drop bUnit's element-reference marker of the label; the field's control is absent, so nothing reads the id, and the root renders nothing more.
/// <summary>The conformance suite on <see cref="FieldLabel"/> inside a <see cref="FieldRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class FieldLabelConformanceTests : PartConformanceTests<FieldLabel>
{
    public FieldLabelConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<FieldRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<FieldLabel> Options { get; } = new("label") { TestRenderPropWith = "label" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(FieldLabel part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<FieldLabel> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<FieldLabelState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<FieldLabel> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<FieldLabelState, string?>)(_ => classValue()));
    }
}
