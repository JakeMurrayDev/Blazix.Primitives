using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>ACC-STR-01, ACC-STA-01: <c>AccordionRoot.test.tsx</c>'s conformance suite with <c>refInstanceof: HTMLDivElement</c>.</summary>
public sealed class AccordionRootConformanceTests : PartConformanceTests<AccordionRoot<string>>
{
    public AccordionRootConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AccordionRoot<string>> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AccordionRoot<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AccordionRoot<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AccordionRootState<string>>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AccordionRoot<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AccordionRootState<string>, string?>)(_ => classValue()));
    }
}

/// <summary>ACC-STR-02: <c>AccordionItem.test.tsx</c>'s conformance suite inside a root (<c>refInstanceof: HTMLDivElement</c>).</summary>
public sealed class AccordionItemConformanceTests : PartConformanceTests<AccordionItem<string>>
{
    public AccordionItemConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        // Base UI's `render: (node) => render(<Accordion.Root>{node}</Accordion.Root>)`.
        Context.RenderTree.Add<AccordionRoot<string>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AccordionItem<string>> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AccordionItem<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AccordionItem<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AccordionItemState<string>>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AccordionItem<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AccordionItemState<string>, string?>)(_ => classValue()));
    }
}

/// <summary>ACC-STR-03: <c>AccordionHeader.test.tsx</c>'s conformance suite inside a root and an item (<c>refInstanceof: HTMLHeadingElement</c>).</summary>
public sealed class AccordionHeaderConformanceTests : PartConformanceTests<AccordionHeader<string>>
{
    public AccordionHeaderConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AccordionRoot<string>>();
        Context.RenderTree.Add<AccordionItem<string>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AccordionHeader<string>> Options { get; } = new("h3");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AccordionHeader<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AccordionHeader<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AccordionHeaderState<string>>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AccordionHeader<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AccordionHeaderState<string>, string?>)(_ => classValue()));
    }
}

/// <summary>
/// ACC-STR-04, ACC-API-13: <c>AccordionTrigger.test.tsx</c>'s conformance suite directly inside an item, with
/// <c>button: true</c>, <c>testRenderPropWith: 'button'</c> and the "with a non-native button" re-run (RND-DOM-49).
/// </summary>
public sealed class AccordionTriggerConformanceTests : PartConformanceTests<AccordionTrigger<string>>
{
    public AccordionTriggerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AccordionRoot<string>>();
        Context.RenderTree.Add<AccordionItem<string>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AccordionTrigger<string>> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AccordionTrigger<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AccordionTrigger<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AccordionTriggerState<string>>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AccordionTrigger<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AccordionTriggerState<string>, string?>)(_ => classValue()));
    }
}

/// <summary>ACC-STR-05, ACC-STA-03: <c>AccordionPanel.test.tsx</c>'s conformance suite, <c>&lt;Accordion.Panel keepMounted /&gt;</c> inside a root and an item.</summary>
public sealed class AccordionPanelConformanceTests : PartConformanceTests<AccordionPanel<string>>
{
    public AccordionPanelConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AccordionRoot<string>>();
        Context.RenderTree.Add<AccordionItem<string>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AccordionPanel<string>> Options { get; } = new("div") { Required = p => p.Add(x => x.KeepMounted, true) };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AccordionPanel<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AccordionPanel<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AccordionPanelState<string>>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AccordionPanel<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AccordionPanelState<string>, string?>)(_ => classValue()));
    }
}
