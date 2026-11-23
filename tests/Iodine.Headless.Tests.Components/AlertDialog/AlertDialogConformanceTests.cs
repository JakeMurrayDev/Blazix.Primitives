using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

// Base UI runs no part suite for AlertDialog: its parts are the Dialog components. The port's are sealed types of their own
// (ADR 08 Amendments 2026-10-27), so each runs the Dialog part's suite under its AlertDialog name.

/// <summary>ADL-STR-01, ADL-API-05: DialogTrigger's conformance suite under the AlertDialog names, inside a closed root (DialogTriggerConformanceTests' reason: bUnit keeps an element's reference capture only from its first render, and an open root renders the trigger again when it claims the open dialog).</summary>
public sealed class AlertDialogTriggerConformanceTests : PartConformanceTests<AlertDialogTrigger<NoPayload>>
{
    public AlertDialogTriggerConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AlertDialogRoot<NoPayload>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AlertDialogTrigger<NoPayload>> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AlertDialogTrigger<NoPayload> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AlertDialogTrigger<NoPayload>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AlertDialogTriggerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AlertDialogTrigger<NoPayload>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AlertDialogTriggerState, string?>)(_ => classValue()));
    }
}

/// <summary>ADL-STR-01: DialogClose's conformance suite under the AlertDialog names, inside an open root.</summary>
public sealed class AlertDialogCloseConformanceTests : PartConformanceTests<AlertDialogClose>
{
    public AlertDialogCloseConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AlertDialogRoot<NoPayload>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AlertDialogClose> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AlertDialogClose part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AlertDialogClose> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AlertDialogCloseState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AlertDialogClose> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AlertDialogCloseState, string?>)(_ => classValue()));
    }
}

/// <summary>ADL-STR-01: DialogPortal's conformance suite under the AlertDialog names, inside an open root (the portal node; the wrapper is the pipeline's host wrapper).</summary>
public sealed class AlertDialogPortalConformanceTests : PartConformanceTests<AlertDialogPortal>
{
    public AlertDialogPortalConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AlertDialogRoot<NoPayload>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AlertDialogPortal> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AlertDialogPortal part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AlertDialogPortal> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AlertDialogPortal> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>ADL-STR-01: DialogBackdrop's conformance suite under the AlertDialog names, inside an open root.</summary>
public sealed class AlertDialogBackdropConformanceTests : PartConformanceTests<AlertDialogBackdrop>
{
    public AlertDialogBackdropConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AlertDialogRoot<NoPayload>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AlertDialogBackdrop> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AlertDialogBackdrop part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AlertDialogBackdrop> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AlertDialogBackdropState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AlertDialogBackdrop> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AlertDialogBackdropState, string?>)(_ => classValue()));
    }
}

/// <summary>ADL-STR-01: DialogViewport's conformance suite under the AlertDialog names, inside a closed root and its kept-mounted portal (DialogViewportConformanceTests' reason).</summary>
public sealed class AlertDialogViewportConformanceTests : PartConformanceTests<AlertDialogViewport>
{
    public AlertDialogViewportConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AlertDialogRoot<NoPayload>>();
        Context.RenderTree.Add<AlertDialogPortal>(p => p.Add(x => x.KeepMounted, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AlertDialogViewport> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AlertDialogViewport part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AlertDialogViewport> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AlertDialogViewportState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AlertDialogViewport> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AlertDialogViewportState, string?>)(_ => classValue()));
    }
}

/// <summary>ADL-STR-01: DialogPopup's conformance suite under the AlertDialog names, inside a closed root and its kept-mounted portal (DialogPopupConformanceTests' reason).</summary>
public sealed class AlertDialogPopupConformanceTests : PartConformanceTests<AlertDialogPopup>
{
    public AlertDialogPopupConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AlertDialogRoot<NoPayload>>();
        Context.RenderTree.Add<AlertDialogPortal>(p => p.Add(x => x.KeepMounted, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AlertDialogPopup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AlertDialogPopup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AlertDialogPopup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<AlertDialogPopupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AlertDialogPopup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<AlertDialogPopupState, string?>)(_ => classValue()));
    }
}

/// <summary>ADL-STR-01: DialogTitle's conformance suite under the AlertDialog names, inside an open root.</summary>
public sealed class AlertDialogTitleConformanceTests : PartConformanceTests<AlertDialogTitle>
{
    public AlertDialogTitleConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AlertDialogRoot<NoPayload>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AlertDialogTitle> Options { get; } = new("h2");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AlertDialogTitle part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AlertDialogTitle> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AlertDialogTitle> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>ADL-STR-01: DialogDescription's conformance suite under the AlertDialog names, inside an open root.</summary>
public sealed class AlertDialogDescriptionConformanceTests : PartConformanceTests<AlertDialogDescription>
{
    public AlertDialogDescriptionConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<AlertDialogRoot<NoPayload>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<AlertDialogDescription> Options { get; } = new("p");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(AlertDialogDescription part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<AlertDialogDescription> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<AlertDialogDescription> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}
