using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// AvatarRoot.test.tsx: describeConformance(<Avatar.Root />, () => ({ render, refInstanceof: window.HTMLSpanElement })).
// AVA-STR-01, AVA-API-01, AVA-DOM-08, AVA-DOM-11 (RND-DOM-36..48; RND-DOM-49 does not apply, no NativeButton).
/// <summary>The conformance suite on <see cref="AvatarRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class AvatarRootConformanceTests : PartConformanceTests<AvatarRoot>
{
    public AvatarRootConformanceTests()
    {
        // The root attaches nothing itself; an image would, and there is none here.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AvatarRoot> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AvatarRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AvatarRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AvatarRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AvatarRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AvatarRootState, string?>)(_ => classValue()));
    }
}
