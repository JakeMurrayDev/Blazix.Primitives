using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// AvatarImage.test.tsx: describeConformance(<Avatar.Image src="test.png" />, () => ({ render: inside <Avatar.Root>,
// refInstanceof: window.HTMLImageElement })). AVA-API-06, AVA-DOM-11. Base UI tests the image in a loaded state with a
// cached source; here it is kept mounted, so the element exists without driving the module (the twins' contract is
// AvatarTwinWiringTests).
/// <summary>The conformance suite on <see cref="AvatarImage"/> inside an <see cref="AvatarRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class AvatarImageConformanceTests : PartConformanceTests<AvatarImage>
{
    public AvatarImageConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AvatarRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AvatarImage> Options { get; } = new("img") { Required = p => p.Add(x => x.Src, "test.png").Add(x => x.KeepMounted, true) };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AvatarImage part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AvatarImage> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AvatarImageState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AvatarImage> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AvatarImageState, string?>)(_ => classValue()));
    }
}
