using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// PreviewCardTrigger.test.tsx: describeConformance(<PreviewCard.Trigger />, () => ({ refInstanceof: HTMLAnchorElement, render inside
// <PreviewCard.Root> })). PRV-STR-02, PRV-API-15.
/// <summary>The conformance suite on <see cref="PreviewCardTrigger{TPayload}"/> inside a <see cref="PreviewCardRoot{TPayload}"/>.</summary>
public sealed class PreviewCardTriggerConformanceTests : PartConformanceTests<PreviewCardTrigger<NoPayload>>
{
    public PreviewCardTriggerConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<PreviewCardRoot<NoPayload>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PreviewCardTrigger<NoPayload>> Options { get; } = new("a") { TestRenderPropWith = "a" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PreviewCardTrigger<NoPayload> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PreviewCardTrigger<NoPayload>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<PreviewCardTriggerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PreviewCardTrigger<NoPayload>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<PreviewCardTriggerState, string?>)(_ => classValue()));
    }
}

// PreviewCardPortal.test.tsx: describeConformance(<PreviewCard.Portal />, () => ({ refInstanceof: HTMLDivElement, render inside an
// open <PreviewCard.Root> })). PRV-STR-03, PRV-STA-03.
/// <summary>The conformance suite on <see cref="PreviewCardPortal"/> inside an open <see cref="PreviewCardRoot{TPayload}"/>.</summary>
public sealed class PreviewCardPortalConformanceTests : PartConformanceTests<PreviewCardPortal>
{
    public PreviewCardPortalConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<PreviewCardRoot<NoPayload>>(p => p.Add(x => x.DefaultOpen, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PreviewCardPortal> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PreviewCardPortal part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PreviewCardPortal> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PreviewCardPortal> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

// PreviewCardPositioner.test.tsx: describeConformance(<PreviewCard.Positioner />, () => ({ refInstanceof: HTMLDivElement, render
// inside an open <PreviewCard.Root><PreviewCard.Portal> })). PRV-STR-04.
/// <summary>The conformance suite on <see cref="PreviewCardPositioner"/> inside an open root and its portal.</summary>
public sealed class PreviewCardPositionerConformanceTests : PartConformanceTests<PreviewCardPositioner>
{
    public PreviewCardPositionerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<PreviewCardRoot<NoPayload>>(p => p.Add(x => x.DefaultOpen, true));
        Context.RenderTree.Add<PreviewCardPortal>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PreviewCardPositioner> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PreviewCardPositioner part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PreviewCardPositioner> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<PreviewCardPositionerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PreviewCardPositioner> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<PreviewCardPositionerState, string?>)(_ => classValue()));
    }
}

// PreviewCardPopup.test.tsx: describeConformance(<PreviewCard.Popup />, () => ({ refInstanceof: HTMLDivElement, render inside an open
// <PreviewCard.Root><PreviewCard.Portal><PreviewCard.Positioner> })). PRV-STR-05.
/// <summary>The conformance suite on <see cref="PreviewCardPopup"/> inside an open root, its portal and its positioner.</summary>
public sealed class PreviewCardPopupConformanceTests : PartConformanceTests<PreviewCardPopup>
{
    public PreviewCardPopupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<PreviewCardRoot<NoPayload>>(p => p.Add(x => x.DefaultOpen, true));
        Context.RenderTree.Add<PreviewCardPortal>();
        Context.RenderTree.Add<PreviewCardPositioner>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PreviewCardPopup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PreviewCardPopup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PreviewCardPopup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<PreviewCardPopupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PreviewCardPopup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<PreviewCardPopupState, string?>)(_ => classValue()));
    }
}

// PreviewCardArrow.test.tsx: describeConformance(<PreviewCard.Arrow />, () => ({ refInstanceof: Element, render inside an open
// <PreviewCard.Root><PreviewCard.Portal><PreviewCard.Positioner><PreviewCard.Popup> })). PRV-STR-06.
/// <summary>The conformance suite on <see cref="PreviewCardArrow"/> inside an open root's popup.</summary>
public sealed class PreviewCardArrowConformanceTests : PartConformanceTests<PreviewCardArrow>
{
    public PreviewCardArrowConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<PreviewCardRoot<NoPayload>>(p => p.Add(x => x.DefaultOpen, true));
        Context.RenderTree.Add<PreviewCardPortal>();
        Context.RenderTree.Add<PreviewCardPositioner>();
        Context.RenderTree.Add<PreviewCardPopup>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PreviewCardArrow> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PreviewCardArrow part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PreviewCardArrow> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<PreviewCardArrowState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PreviewCardArrow> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<PreviewCardArrowState, string?>)(_ => classValue()));
    }
}

// PreviewCardViewport.test.tsx: describeConformance(<PreviewCard.Viewport />, () => ({ refInstanceof: HTMLDivElement, render inside
// an open <PreviewCard.Root><PreviewCard.Portal><PreviewCard.Positioner><PreviewCard.Popup> })). PRV-STR-08.
/// <summary>The conformance suite on <see cref="PreviewCardViewport"/> inside an open root's popup.</summary>
public sealed class PreviewCardViewportConformanceTests : PartConformanceTests<PreviewCardViewport>
{
    public PreviewCardViewportConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<PreviewCardRoot<NoPayload>>(p => p.Add(x => x.DefaultOpen, true));
        Context.RenderTree.Add<PreviewCardPortal>();
        Context.RenderTree.Add<PreviewCardPositioner>();
        Context.RenderTree.Add<PreviewCardPopup>();
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PreviewCardViewport> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PreviewCardViewport part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PreviewCardViewport> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<PreviewCardViewportState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PreviewCardViewport> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<PreviewCardViewportState, string?>)(_ => classValue()));
    }
}

// PreviewCardBackdrop.test.tsx: describeConformance(<PreviewCard.Backdrop />, () => ({ refInstanceof: HTMLDivElement, render
// directly inside <PreviewCard.Root open> })). PRV-STR-07, PRV-STA-08.
/// <summary>The conformance suite on <see cref="PreviewCardBackdrop"/> directly inside an open root.</summary>
public sealed class PreviewCardBackdropConformanceTests : PartConformanceTests<PreviewCardBackdrop>
{
    public PreviewCardBackdropConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<PreviewCardRoot<NoPayload>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<PreviewCardBackdrop> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(PreviewCardBackdrop part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<PreviewCardBackdrop> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<PreviewCardBackdropState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<PreviewCardBackdrop> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<PreviewCardBackdropState, string?>)(_ => classValue()));
    }
}
