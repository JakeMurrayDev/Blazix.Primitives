using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>COL-STR-01: <c>CollapsibleRoot.test.tsx</c>'s conformance suite with <c>refInstanceof: HTMLDivElement</c>.</summary>
public sealed class CollapsibleRootConformanceTests : PartConformanceTests<CollapsibleRoot>
{
    public CollapsibleRootConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<CollapsibleRoot> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(CollapsibleRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<CollapsibleRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<CollapsibleRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<CollapsibleRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<CollapsibleRootState, string?>)(_ => classValue()));
    }
}

/// <summary>
/// COL-STR-02, COL-API-05: <c>CollapsibleTrigger.test.tsx</c>'s conformance suite inside a root, with
/// <c>button: true</c>, <c>testRenderPropWith: 'button'</c> and the "with a non-native button" re-run (RND-DOM-49).
/// </summary>
public sealed class CollapsibleTriggerConformanceTests : PartConformanceTests<CollapsibleTrigger>
{
    public CollapsibleTriggerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        // Base UI's `render: (node) => render(<Collapsible.Root>{node}</Collapsible.Root>)`.
        Context.RenderTree.Add<CollapsibleRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<CollapsibleTrigger> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(CollapsibleTrigger part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<CollapsibleTrigger> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<CollapsibleTriggerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<CollapsibleTrigger> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<CollapsibleTriggerState, string?>)(_ => classValue()));
    }
}

/// <summary>COL-STR-03: <c>CollapsiblePanel.test.tsx</c>'s conformance suite inside <c>Root defaultOpen</c>.</summary>
public sealed class CollapsiblePanelConformanceTests : PartConformanceTests<CollapsiblePanel>
{
    public CollapsiblePanelConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<CollapsibleRoot>(p => p.Add(x => x.DefaultOpen, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<CollapsiblePanel> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(CollapsiblePanel part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<CollapsiblePanel> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<CollapsiblePanelState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<CollapsiblePanel> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<CollapsiblePanelState, string?>)(_ => classValue()));
    }
}
