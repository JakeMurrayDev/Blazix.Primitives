using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>POP-STR-03, POP-API-11: <c>PopoverTrigger.test.tsx</c>'s conformance suite inside a root, <c>button: true</c>, <c>testRenderPropWith: 'button'</c>.</summary>
public sealed class PopoverTriggerConformanceTests : PartConformanceTests<PopoverTrigger<NoPayload>>
{
    public PopoverTriggerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<PopoverRoot<NoPayload>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PopoverTrigger<NoPayload>> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PopoverTrigger<NoPayload> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PopoverTrigger<NoPayload>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<PopoverTriggerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PopoverTrigger<NoPayload>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<PopoverTriggerState, string?>)(_ => classValue()));
    }
}

/// <summary>POP-STR-04, POP-DOM-09: <c>PopoverPortal.test.tsx</c>'s conformance suite under <c>Root open</c>.</summary>
public sealed class PopoverPortalConformanceTests : PartConformanceTests<PopoverPortal>
{
    public PopoverPortalConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<PopoverRoot<NoPayload>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PopoverPortal> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PopoverPortal part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PopoverPortal> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PopoverPortal> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>POP-STR-05: <c>PopoverPositioner.test.tsx</c>'s conformance suite under <c>Root open</c> and a Portal.</summary>
public sealed class PopoverPositionerConformanceTests : PartConformanceTests<PopoverPositioner>
{
    public PopoverPositionerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<PopoverRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<PopoverPortal>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PopoverPositioner> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PopoverPositioner part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PopoverPositioner> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<PopoverPositionerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PopoverPositioner> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<PopoverPositionerState, string?>)(_ => classValue()));
    }
}

/// <summary>POP-STR-06: <c>PopoverPopup.test.tsx</c>'s conformance suite under <c>Root open</c>, a Portal and a Positioner.</summary>
public sealed class PopoverPopupConformanceTests : PartConformanceTests<PopoverPopup>
{
    public PopoverPopupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<PopoverRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<PopoverPortal>();
        Context.RenderTree.Add<PopoverPositioner>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PopoverPopup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PopoverPopup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PopoverPopup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<PopoverPopupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PopoverPopup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<PopoverPopupState, string?>)(_ => classValue()));
    }
}

/// <summary>POP-STR-07: <c>PopoverArrow.test.tsx</c>'s conformance suite inside an open Popup.</summary>
public sealed class PopoverArrowConformanceTests : PartConformanceTests<PopoverArrow>
{
    public PopoverArrowConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        PopoverConformance.AddOpenPopup(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PopoverArrow> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PopoverArrow part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PopoverArrow> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<PopoverArrowState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PopoverArrow> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<PopoverArrowState, string?>)(_ => classValue()));
    }
}

/// <summary>POP-STR-08: <c>PopoverBackdrop.test.tsx</c>'s conformance suite directly under <c>Root open</c>.</summary>
public sealed class PopoverBackdropConformanceTests : PartConformanceTests<PopoverBackdrop>
{
    public PopoverBackdropConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<PopoverRoot<NoPayload>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PopoverBackdrop> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PopoverBackdrop part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PopoverBackdrop> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<PopoverBackdropState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PopoverBackdrop> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<PopoverBackdropState, string?>)(_ => classValue()));
    }
}

/// <summary>POP-STR-09: <c>PopoverViewport.test.tsx</c>'s conformance suite inside an open Popup.</summary>
public sealed class PopoverViewportConformanceTests : PartConformanceTests<PopoverViewport>
{
    public PopoverViewportConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        PopoverConformance.AddOpenPopup(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PopoverViewport> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PopoverViewport part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PopoverViewport> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<PopoverViewportState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PopoverViewport> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<PopoverViewportState, string?>)(_ => classValue()));
    }
}

/// <summary>POP-STR-10: <c>PopoverTitle.test.tsx</c>'s conformance suite inside an open Popup (<c>refInstanceof: HTMLHeadingElement</c>).</summary>
public sealed class PopoverTitleConformanceTests : PartConformanceTests<PopoverTitle>
{
    public PopoverTitleConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        PopoverConformance.AddOpenPopup(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PopoverTitle> Options { get; } = new("h2");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PopoverTitle part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PopoverTitle> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PopoverTitle> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>POP-STR-10: <c>PopoverDescription.test.tsx</c>'s conformance suite inside an open Popup (<c>refInstanceof: HTMLParagraphElement</c>).</summary>
public sealed class PopoverDescriptionConformanceTests : PartConformanceTests<PopoverDescription>
{
    public PopoverDescriptionConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        PopoverConformance.AddOpenPopup(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PopoverDescription> Options { get; } = new("p");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PopoverDescription part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PopoverDescription> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PopoverDescription> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>POP-STR-11, POP-API-24, POP-DOM-43: <c>PopoverClose.test.tsx</c>'s conformance suite inside an open Popup, <c>button: true</c>.</summary>
public sealed class PopoverCloseConformanceTests : PartConformanceTests<PopoverClose>
{
    public PopoverCloseConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        PopoverConformance.AddOpenPopup(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PopoverClose> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PopoverClose part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PopoverClose> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PopoverClose> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>The wrappers Base UI's conformance renders put around a part that lives in an open popup.</summary>
internal static class PopoverConformance
{
    /// <summary>Wraps the part in <c>Root open</c>, a Portal, a Positioner and a Popup.</summary>
    /// <param name="context">The test's context.</param>
    public static void AddOpenPopup(BunitContext context)
    {
        context.RenderTree.Add<PopoverRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        context.RenderTree.Add<PopoverPortal>();
        context.RenderTree.Add<PopoverPositioner>();
        context.RenderTree.Add<PopoverPopup>();
        context.RenderTree.Add<RenderedOnce>();
    }
}

/// <summary>
/// A test-only wrapper that renders its content once. A part inside the Popup makes the Popup render again in its mount
/// batch (a Title's id, a Close's count), which re-renders every child holding a fragment (ADR 16 Amendments 2026-10-27);
/// Blazor never copies an element's reference capture into the re-rendered frame, so bUnit's markup drops the id the
/// ref titles look up. The browser keeps the element, which the L4 conformance titles check; here the part under test
/// renders once, as React leaves an unchanged child alone.
/// </summary>
internal sealed class RenderedOnce : ComponentBase
{
    private bool rendered;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override bool ShouldRender() => !rendered;

    /// <inheritdoc />
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        rendered = true;
        builder.AddContent(0, ChildContent);
    }
}
