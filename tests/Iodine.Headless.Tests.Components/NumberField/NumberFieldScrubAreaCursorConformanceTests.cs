using Iodine.Headless.Rendering;
using Iodine.Headless.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// NumberFieldScrubAreaCursor.test.tsx: describeConformance(<NumberField.ScrubAreaCursor />, () => ({ refInstanceof: window.HTMLSpanElement,
// render: inside a Root and a ScrubArea with a forced scrubbing context })). NUM-DOM-13, NUM-API-20, NUM-API-21, NUM-API-25.
/// <summary>The conformance suite on <see cref="NumberFieldScrubAreaCursor"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class NumberFieldScrubAreaCursorConformanceTests : PartConformanceTests<NumberFieldScrubAreaCursor>
{
    public NumberFieldScrubAreaCursorConformanceTests()
    {
        // The twins' imports and attaches are not the subject here.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NumberFieldRoot>();
        // NumberFieldScrubAreaCursor.test.tsx:20-27: Base UI forces a scrubbing scrub area context around the cursor.
        NumberFieldScrubAreaContext scrubbing = new();
        scrubbing.SetScrubbing(true, cursorAllowed: true);
        Context.RenderTree.Add<CascadingValue<NumberFieldScrubAreaContext>>(p => p.Add(x => x.Value, scrubbing).Add(x => x.IsFixed, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NumberFieldScrubAreaCursor> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NumberFieldScrubAreaCursor part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NumberFieldScrubAreaCursor> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NumberFieldScrubAreaCursorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NumberFieldScrubAreaCursor> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NumberFieldScrubAreaCursorState, string?>)(_ => classValue()));
    }
}
