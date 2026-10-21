using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace: a `Button` namespace would hide the `Button` type.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// BTN-DOM-06, BTN-API-04, BTN-STR-01: <c>Button.test.tsx</c>'s conformance suite "Base UI component API" with
/// <c>refInstanceof: HTMLButtonElement</c> and <c>button: true</c>; <c>testRenderPropWith</c> stays <c>div</c>, so every
/// substitution passes <c>NativeButton="false"</c> and RND-DOM-49 is met by the same runs.
/// </summary>
public sealed class ButtonConformanceTests : PartConformanceTests<Button>
{
    public ButtonConformanceTests()
    {
        // The twin's import and attach are not the subject here; the twin contract tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<Button> Options { get; } = new("button") { Button = true };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(Button part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<Button> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ButtonState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<Button> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ButtonState, string?>)(_ => classValue()));
    }
}
