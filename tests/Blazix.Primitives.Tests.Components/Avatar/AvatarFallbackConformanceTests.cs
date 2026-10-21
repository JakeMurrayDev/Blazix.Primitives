using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// AvatarFallback.test.tsx: describeConformance(<Avatar.Fallback />, () => ({ render: inside <Avatar.Root>,
// refInstanceof: window.HTMLSpanElement })). AVA-API-08, AVA-DOM-10, AVA-DOM-11; the root's status is 'idle'.
/// <summary>The conformance suite on <see cref="AvatarFallback"/> inside an <see cref="AvatarRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class AvatarFallbackConformanceTests : PartConformanceTests<AvatarFallback>
{
    public AvatarFallbackConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AvatarRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AvatarFallback> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AvatarFallback part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AvatarFallback> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AvatarFallbackState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AvatarFallback> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AvatarFallbackState, string?>)(_ => classValue()));
    }
}
