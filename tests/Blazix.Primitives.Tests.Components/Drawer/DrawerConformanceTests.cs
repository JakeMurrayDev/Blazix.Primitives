using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

// Base UI runs describeConformance for Drawer.Popup, Viewport, Content and SwipeArea (SwipeArea is not built yet). The port's
// Trigger, Close, Title, Description, Backdrop, Indent and IndentBackground are sealed types of their own (decision 20 (a) of
// the M6 plan), so each runs the suite too, the AlertDialog precedent; the borrowed parts inside the trees Dialog's suite
// uses. A part inside an open root renders again on mount, and bUnit keeps an element's reference capture only from its
// first render, so the Viewport, Popup and Content run inside a closed root with the portal kept mounted
// (DialogViewportConformanceTests' reason).

/// <summary>DRW-STR-01, DRW-STR-03: Dialog.Trigger's conformance suite under the Drawer name, inside a closed root, with <c>button: true</c>.</summary>
public sealed class DrawerTriggerConformanceTests : PartConformanceTests<DrawerTrigger<NoPayload>>
{
    public DrawerTriggerConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<DrawerRoot<NoPayload>>(p => p.Add(x => x.Modal, (ModalValue?)false));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DrawerTrigger<NoPayload>> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DrawerTrigger<NoPayload> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DrawerTrigger<NoPayload>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<DrawerTriggerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DrawerTrigger<NoPayload>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<DrawerTriggerState, string?>)(_ => classValue()));
    }
}

/// <summary>DRW-STR-01, DRW-STR-03: Dialog.Close's conformance suite under the Drawer name, inside an open root, with <c>button: true</c>.</summary>
public sealed class DrawerCloseConformanceTests : PartConformanceTests<DrawerClose>
{
    public DrawerCloseConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<DrawerRoot<NoPayload>>(p => p.Add(x => x.Open, true).Add(x => x.Modal, (ModalValue?)false));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DrawerClose> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DrawerClose part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DrawerClose> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<DrawerCloseState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DrawerClose> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<DrawerCloseState, string?>)(_ => classValue()));
    }
}

/// <summary>DRW-STR-01, DRW-STR-03: Dialog.Title's conformance suite under the Drawer name, inside an open root.</summary>
public sealed class DrawerTitleConformanceTests : PartConformanceTests<DrawerTitle>
{
    public DrawerTitleConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<DrawerRoot<NoPayload>>(p => p.Add(x => x.Open, true).Add(x => x.Modal, (ModalValue?)false));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DrawerTitle> Options { get; } = new("h2");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DrawerTitle part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DrawerTitle> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DrawerTitle> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>DRW-STR-01, DRW-STR-03: Dialog.Description's conformance suite under the Drawer name, inside an open root.</summary>
public sealed class DrawerDescriptionConformanceTests : PartConformanceTests<DrawerDescription>
{
    public DrawerDescriptionConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<DrawerRoot<NoPayload>>(p => p.Add(x => x.Open, true).Add(x => x.Modal, (ModalValue?)false));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DrawerDescription> Options { get; } = new("p");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DrawerDescription part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DrawerDescription> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DrawerDescription> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>DRW-STR-03: Dialog.Backdrop's conformance suite under the Drawer name, inside an open root.</summary>
public sealed class DrawerBackdropConformanceTests : PartConformanceTests<DrawerBackdrop>
{
    public DrawerBackdropConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<DrawerRoot<NoPayload>>(p => p.Add(x => x.Open, true).Add(x => x.Modal, (ModalValue?)false));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DrawerBackdrop> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DrawerBackdrop part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DrawerBackdrop> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<DrawerBackdropState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DrawerBackdrop> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<DrawerBackdropState, string?>)(_ => classValue()));
    }
}

/// <summary>DRW-STR-04, DRW-API-13: <c>DrawerViewport.test.tsx</c>'s conformance suite (<c>Root open &gt; Portal</c> in Base UI), inside a closed root and its kept-mounted portal.</summary>
public sealed class DrawerViewportConformanceTests : PartConformanceTests<DrawerViewport>
{
    public DrawerViewportConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<DrawerRoot<NoPayload>>(p => p.Add(x => x.Modal, (ModalValue?)false));
        Context.RenderTree.Add<DrawerPortal>(p => p.Add(x => x.KeepMounted, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DrawerViewport> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DrawerViewport part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DrawerViewport> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<DrawerViewportState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DrawerViewport> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<DrawerViewportState, string?>)(_ => classValue()));
    }
}

/// <summary>DRW-STR-05, DRW-STA-01, DRW-DOM-15: <c>DrawerPopup.test.tsx</c>'s conformance suite (<c>Root open &gt; Portal &gt; Viewport</c> in Base UI), inside a closed root, its kept-mounted portal and a viewport.</summary>
public sealed class DrawerPopupConformanceTests : PartConformanceTests<DrawerPopup>
{
    public DrawerPopupConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<DrawerRoot<NoPayload>>(p => p.Add(x => x.Modal, (ModalValue?)false));
        Context.RenderTree.Add<DrawerPortal>(p => p.Add(x => x.KeepMounted, true));
        Context.RenderTree.Add<DrawerViewport>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DrawerPopup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DrawerPopup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DrawerPopup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<DrawerPopupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DrawerPopup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<DrawerPopupState, string?>)(_ => classValue()));
    }
}

/// <summary>DRW-STR-06, DRW-API-13: <c>DrawerContent.test.tsx</c>'s conformance suite (<c>Root open &gt; Portal &gt; Viewport &gt; Popup</c> in Base UI), inside a closed root, its kept-mounted portal, a viewport and a popup.</summary>
public sealed class DrawerContentConformanceTests : PartConformanceTests<DrawerContent>
{
    public DrawerContentConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<DrawerRoot<NoPayload>>(p => p.Add(x => x.Modal, (ModalValue?)false));
        Context.RenderTree.Add<DrawerPortal>(p => p.Add(x => x.KeepMounted, true));
        Context.RenderTree.Add<DrawerViewport>();
        Context.RenderTree.Add<DrawerPopup>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DrawerContent> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DrawerContent part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DrawerContent> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DrawerContent> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>DRW-STR-08, DRW-API-13: the conformance suite of the indent, which renders without a provider (Base UI runs none for it).</summary>
public sealed class DrawerIndentConformanceTests : PartConformanceTests<DrawerIndent>
{
    public DrawerIndentConformanceTests()
    {
        // No root and no provider: the indent parts render alone (DRW-STR-08) and load no module.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DrawerIndent> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DrawerIndent part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DrawerIndent> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<DrawerIndentState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DrawerIndent> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<DrawerIndentState, string?>)(_ => classValue()));
    }
}

/// <summary>DRW-STR-08, DRW-API-13: the conformance suite of the indent background, which renders without a provider (Base UI runs none for it).</summary>
public sealed class DrawerIndentBackgroundConformanceTests : PartConformanceTests<DrawerIndentBackground>
{
    public DrawerIndentBackgroundConformanceTests()
    {
        // No root and no provider: the indent parts render alone (DRW-STR-08) and load no module.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DrawerIndentBackground> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DrawerIndentBackground part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DrawerIndentBackground> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<DrawerIndentBackgroundState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DrawerIndentBackground> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<DrawerIndentBackgroundState, string?>)(_ => classValue()));
    }
}
