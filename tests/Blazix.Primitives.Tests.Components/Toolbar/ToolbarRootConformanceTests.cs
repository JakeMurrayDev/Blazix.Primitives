using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ToolbarRoot.test.tsx: describeConformance(<Toolbar.Root />, () => ({ refInstanceof: window.HTMLDivElement })).
// TLB-DOM-12, TLB-API-04, TLB-API-05, TLB-DOM-11.
/// <summary>The conformance suite on <see cref="ToolbarRoot"/>: the shared RND-DOM-36..48 tests with Base UI's titles.</summary>
public sealed class ToolbarRootConformanceTests : PartConformanceTests<ToolbarRoot>
{
    public ToolbarRootConformanceTests()
    {
        // The composite twin's import and attach are not the subject here; ToolbarRootTests records them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToolbarRoot> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToolbarRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToolbarRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToolbarRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToolbarRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToolbarRootState, string?>)(_ => classValue()));
    }
}
