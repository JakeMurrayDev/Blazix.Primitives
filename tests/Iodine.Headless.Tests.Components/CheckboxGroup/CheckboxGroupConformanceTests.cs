using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// CheckboxGroup.test.tsx: describeConformance(<CheckboxGroup />, () => ({ refInstanceof: window.HTMLDivElement })).
// CKG-DOM-07, CKG-API-07.
/// <summary>The conformance suite on <see cref="CheckboxGroup"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class CheckboxGroupConformanceTests : PartConformanceTests<CheckboxGroup>
{
    public CheckboxGroupConformanceTests()
    {
        // The group attaches no twin; Loose keeps a member-free render free of interop setup.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<CheckboxGroup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(CheckboxGroup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<CheckboxGroup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<CheckboxGroupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<CheckboxGroup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<CheckboxGroupState, string?>)(_ => classValue()));
    }
}
