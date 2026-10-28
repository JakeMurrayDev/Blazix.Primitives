using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>DLG-STR-02, DLG-API-15: <c>DialogTrigger.test.tsx</c>'s conformance suite inside a root, with <c>button: true</c> and <c>testRenderPropWith: 'button'</c> (RND-DOM-49). Base UI renders it under <c>Root open modal={false}</c>; here the root is closed (the portal kept mounted), because bUnit's markup keeps an element's reference capture only from its first render and an open root renders the part again on mount (a lone trigger claims the open dialog; a non-modal open popup's portal renders its outside guards).</summary>
public sealed class DialogTriggerConformanceTests : PartConformanceTests<DialogTrigger<NoPayload>>
{
    public DialogTriggerConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<DialogRoot<NoPayload>>(p => p.Add(x => x.Modal, (ModalValue?)false));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DialogTrigger<NoPayload>> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DialogTrigger<NoPayload> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DialogTrigger<NoPayload>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<DialogTriggerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DialogTrigger<NoPayload>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<DialogTriggerState, string?>)(_ => classValue()));
    }
}

/// <summary>DLG-STR-09, DLG-API-22: <c>DialogClose.test.tsx</c>'s conformance suite inside an open root, with <c>button: true</c> and <c>testRenderPropWith: 'button'</c>.</summary>
public sealed class DialogCloseConformanceTests : PartConformanceTests<DialogClose>
{
    public DialogCloseConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        // Base UI's `render: (node) => render(<Dialog.Root open modal={false}>{node}</Dialog.Root>)`.
        Context.RenderTree.Add<DialogRoot<NoPayload>>(p => p.Add(x => x.Open, true).Add(x => x.Modal, (ModalValue?)false));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DialogClose> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DialogClose part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DialogClose> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<DialogCloseState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DialogClose> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<DialogCloseState, string?>)(_ => classValue()));
    }
}

/// <summary>DLG-STR-03, DLG-DOM-05: <c>DialogPortal.test.tsx</c>'s conformance suite inside an open root (the portal node; the wrapper is the pipeline's host wrapper).</summary>
public sealed class DialogPortalConformanceTests : PartConformanceTests<DialogPortal>
{
    public DialogPortalConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        // Base UI's `render: (node) => render(<Dialog.Root open>{node}</Dialog.Root>)`.
        Context.RenderTree.Add<DialogRoot<NoPayload>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DialogPortal> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DialogPortal part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DialogPortal> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DialogPortal> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>DLG-STR-05: <c>DialogBackdrop.test.tsx</c>'s conformance suite inside an open root.</summary>
public sealed class DialogBackdropConformanceTests : PartConformanceTests<DialogBackdrop>
{
    public DialogBackdropConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        // Base UI's `render: (node) => render(<Dialog.Root open modal={false}>{node}</Dialog.Root>)`.
        Context.RenderTree.Add<DialogRoot<NoPayload>>(p => p.Add(x => x.Open, true).Add(x => x.Modal, (ModalValue?)false));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DialogBackdrop> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DialogBackdrop part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DialogBackdrop> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<DialogBackdropState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DialogBackdrop> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<DialogBackdropState, string?>)(_ => classValue()));
    }
}

/// <summary>DLG-STR-06: <c>DialogViewport.test.tsx</c>'s conformance suite inside a root and its portal. Base UI renders it under <c>Root open modal={false}</c>; here the root is closed (the portal kept mounted), because bUnit's markup keeps an element's reference capture only from its first render and an open root renders the part again on mount (a lone trigger claims the open dialog; a non-modal open popup's portal renders its outside guards).</summary>
public sealed class DialogViewportConformanceTests : PartConformanceTests<DialogViewport>
{
    public DialogViewportConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<DialogRoot<NoPayload>>(p => p.Add(x => x.Modal, (ModalValue?)false));
        Context.RenderTree.Add<DialogPortal>(p => p.Add(x => x.KeepMounted, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DialogViewport> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DialogViewport part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DialogViewport> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<DialogViewportState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DialogViewport> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<DialogViewportState, string?>)(_ => classValue()));
    }
}

/// <summary>DLG-STR-07: <c>DialogPopup.test.tsx</c>'s conformance suite inside a root and its portal. Base UI renders it under <c>Root open modal={false}</c>; here the root is closed (the portal kept mounted), because bUnit's markup keeps an element's reference capture only from its first render and an open root renders the part again on mount (a lone trigger claims the open dialog; a non-modal open popup's portal renders its outside guards).</summary>
public sealed class DialogPopupConformanceTests : PartConformanceTests<DialogPopup>
{
    public DialogPopupConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<DialogRoot<NoPayload>>(p => p.Add(x => x.Modal, (ModalValue?)false));
        Context.RenderTree.Add<DialogPortal>(p => p.Add(x => x.KeepMounted, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DialogPopup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DialogPopup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DialogPopup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<DialogPopupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DialogPopup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<DialogPopupState, string?>)(_ => classValue()));
    }
}

/// <summary>DLG-STR-08: <c>DialogTitle.test.tsx</c>'s conformance suite inside an open root.</summary>
public sealed class DialogTitleConformanceTests : PartConformanceTests<DialogTitle>
{
    public DialogTitleConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        // Base UI's `render: (node) => render(<Dialog.Root open modal={false}>{node}</Dialog.Root>)`.
        Context.RenderTree.Add<DialogRoot<NoPayload>>(p => p.Add(x => x.Open, true).Add(x => x.Modal, (ModalValue?)false));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DialogTitle> Options { get; } = new("h2");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DialogTitle part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DialogTitle> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DialogTitle> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>DLG-STR-08: <c>DialogDescription.test.tsx</c>'s conformance suite inside an open root.</summary>
public sealed class DialogDescriptionConformanceTests : PartConformanceTests<DialogDescription>
{
    public DialogDescriptionConformanceTests()
    {
        // The twins' import and attach are not the subject here; the twin wiring tests record them.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        // Base UI's `render: (node) => render(<Dialog.Root open modal={false}>{node}</Dialog.Root>)`.
        Context.RenderTree.Add<DialogRoot<NoPayload>>(p => p.Add(x => x.Open, true).Add(x => x.Modal, (ModalValue?)false));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<DialogDescription> Options { get; } = new("p");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(DialogDescription part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<DialogDescription> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<DialogDescription> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}
