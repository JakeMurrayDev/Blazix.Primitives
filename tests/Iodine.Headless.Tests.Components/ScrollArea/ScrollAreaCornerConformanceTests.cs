using Iodine.Headless.Rendering;
using Iodine.Headless.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// ScrollAreaCorner.test.tsx: describeConformance(<ScrollArea.Corner />, () => ({ render inside a Root whose viewport overflows
// on both axes, refInstanceof: window.HTMLDivElement })). SCR-STR-06. The overflow is applied by a stand-in for the
// measurement report (ScrollAreaBothAxesOverflow), since the suite renders the part fresh in every test.
/// <summary>The conformance suite on <see cref="ScrollAreaCorner"/> inside a <see cref="ScrollAreaRoot"/> whose content overflows on both axes.</summary>
public sealed class ScrollAreaCornerConformanceTests : PartConformanceTests<ScrollAreaCorner>
{
    public ScrollAreaCornerConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ScrollAreaRoot>();
        Context.RenderTree.Add<ScrollAreaBothAxesOverflow>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ScrollAreaCorner> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ScrollAreaCorner part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ScrollAreaCorner> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ScrollAreaCornerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ScrollAreaCorner> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ScrollAreaCornerState, string?>)(_ => classValue()));
    }
}
