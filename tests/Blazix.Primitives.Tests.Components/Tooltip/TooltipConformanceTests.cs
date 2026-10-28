using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// TooltipTrigger.test.tsx: describeConformance(<Tooltip.Trigger />, () => ({ refInstanceof: HTMLButtonElement, render inside
// <Tooltip.Root> })); no `button` option (TIP §16 Q10). TIP-STR-03, TIP-DOM-01.
/// <summary>The conformance suite on <see cref="TooltipTrigger{TPayload}"/> inside a <see cref="TooltipRoot{TPayload}"/>.</summary>
public sealed class TooltipTriggerConformanceTests : PartConformanceTests<TooltipTrigger<NoPayload>>
{
    public TooltipTriggerConformanceTests()
    {
        // The twins' imports and attaches are not the subject here; the family's tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<TooltipRoot<NoPayload>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<TooltipTrigger<NoPayload>> Options { get; } = new("button") { TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(TooltipTrigger<NoPayload> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<TooltipTrigger<NoPayload>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<TooltipTriggerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<TooltipTrigger<NoPayload>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<TooltipTriggerState, string?>)(_ => classValue()));
    }
}

// TooltipPortal.test.tsx: describeConformance(<Tooltip.Portal />, () => ({ refInstanceof: HTMLDivElement, render inside an
// open <Tooltip.Root> })). TIP-STR-04, TIP-STA-14 (the class function receives the empty state).
/// <summary>The conformance suite on <see cref="TooltipPortal"/> inside an open <see cref="TooltipRoot{TPayload}"/>.</summary>
public sealed class TooltipPortalConformanceTests : PartConformanceTests<TooltipPortal>
{
    public TooltipPortalConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<TooltipRoot<NoPayload>>(p => p.Add(x => x.DefaultOpen, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<TooltipPortal> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(TooltipPortal part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<TooltipPortal> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<TooltipPortal> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

// TooltipPositioner.test.tsx: describeConformance(<Tooltip.Positioner />, () => ({ refInstanceof: HTMLDivElement, render
// inside an open <Tooltip.Root><Tooltip.Portal> })). TIP-STR-05.
/// <summary>The conformance suite on <see cref="TooltipPositioner"/> inside an open root and its portal.</summary>
public sealed class TooltipPositionerConformanceTests : PartConformanceTests<TooltipPositioner>
{
    public TooltipPositionerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<TooltipRoot<NoPayload>>(p => p.Add(x => x.DefaultOpen, true));
        Context.RenderTree.Add<TooltipPortal>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<TooltipPositioner> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(TooltipPositioner part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<TooltipPositioner> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<TooltipPositionerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<TooltipPositioner> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<TooltipPositionerState, string?>)(_ => classValue()));
    }
}

// TooltipPopup.test.tsx: describeConformance(<Tooltip.Popup />, () => ({ refInstanceof: HTMLDivElement, render inside an open
// <Tooltip.Root><Tooltip.Portal><Tooltip.Positioner> })). TIP-STR-06.
/// <summary>The conformance suite on <see cref="TooltipPopup"/> inside an open root, its portal and its positioner.</summary>
public sealed class TooltipPopupConformanceTests : PartConformanceTests<TooltipPopup>
{
    public TooltipPopupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<TooltipRoot<NoPayload>>(p => p.Add(x => x.DefaultOpen, true));
        Context.RenderTree.Add<TooltipPortal>();
        Context.RenderTree.Add<TooltipPositioner>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<TooltipPopup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(TooltipPopup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<TooltipPopup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<TooltipPopupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<TooltipPopup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<TooltipPopupState, string?>)(_ => classValue()));
    }
}

// TooltipArrow.test.tsx: describeConformance(<Tooltip.Arrow />, () => ({ refInstanceof: Element, render inside an open
// <Tooltip.Root><Tooltip.Portal><Tooltip.Positioner><Tooltip.Popup> })). TIP-STR-07.
/// <summary>The conformance suite on <see cref="TooltipArrow"/> inside an open root's popup.</summary>
public sealed class TooltipArrowConformanceTests : PartConformanceTests<TooltipArrow>
{
    public TooltipArrowConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<TooltipRoot<NoPayload>>(p => p.Add(x => x.DefaultOpen, true));
        Context.RenderTree.Add<TooltipPortal>();
        Context.RenderTree.Add<TooltipPositioner>();
        Context.RenderTree.Add<TooltipPopup>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<TooltipArrow> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(TooltipArrow part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<TooltipArrow> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<TooltipArrowState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<TooltipArrow> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<TooltipArrowState, string?>)(_ => classValue()));
    }
}
