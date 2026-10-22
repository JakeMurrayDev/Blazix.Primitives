using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// FieldError.test.tsx: describeConformance(<Field.Error match />, () => ({ refInstanceof: window.HTMLDivElement, render: inside <Field.Root invalid> })). FLD-DOM-19, FLD-API-33/34.
/// <summary>
/// The conformance suite on <see cref="FieldError"/> with <see cref="FieldError.Match"/> <see langword="true"/>, inside an
/// invalid <see cref="FieldRoot"/> so that it is mounted: the shared RND-DOM-36..49 tests with Base UI's titles.
/// </summary>
public sealed class FieldErrorConformanceTests : PartConformanceTests<FieldError>
{
    public FieldErrorConformanceTests()
    {
        // The twins' import and attach are not the subject here; the field error tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<FieldRoot>(p => p.Add(x => x.Invalid, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<FieldError> Options { get; } = new("div") { Required = p => p.Add(x => x.Match, true) };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(FieldError part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<FieldError> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<FieldErrorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<FieldError> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<FieldErrorState, string?>)(_ => classValue()));
    }
}
