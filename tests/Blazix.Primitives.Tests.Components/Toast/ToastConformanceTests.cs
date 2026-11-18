using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// The toast every conformance run renders under, with the content Title, Description and Action need to render.
internal static class ToastConformance
{
    public static readonly ToastObject Toast = new() { Id = "test", Title = "title", Description = "description" };

    public static void Prepare(BunitContext context)
    {
        // The twins' imports and attaches are not the subject here; an interactive renderer, so the provider subscribes.
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Renderer.SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }
}

// ToastViewport.test.tsx: describeConformance(<Toast.Viewport />, () => ({ refInstanceof: HTMLDivElement, render inside <Toast.Provider> })). TST-STR-02, TST-DOM-23, TST-STA-26.
/// <summary>The conformance suite on <see cref="ToastViewport{TData}"/>.</summary>
public sealed class ToastViewportConformanceTests : PartConformanceTests<ToastViewport<NoPayload>>
{
    public ToastViewportConformanceTests()
    {
        ToastConformance.Prepare(Context);
        Context.RenderTree.Add<ToastProvider<NoPayload>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToastViewport<NoPayload>> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToastViewport<NoPayload> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToastViewport<NoPayload>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToastViewportState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToastViewport<NoPayload>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToastViewportState, string?>)(_ => classValue()));
    }
}

// ToastRoot.test.tsx: describeConformance(<Toast.Root toast={toast} />, () => ({ refInstanceof: HTMLDivElement, render inside <Toast.Provider><Toast.Viewport> })). TST-STR-03, TST-STA-26.
/// <summary>The conformance suite on <see cref="ToastRoot{TData}"/>.</summary>
public sealed class ToastRootConformanceTests : PartConformanceTests<ToastRoot<NoPayload>>
{
    public ToastRootConformanceTests()
    {
        ToastConformance.Prepare(Context);
        Context.RenderTree.Add<ToastProvider<NoPayload>>();
        Context.RenderTree.Add<ToastViewport<NoPayload>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToastRoot<NoPayload>> Options { get; } = new("div") { Required = p => p.Add(x => x.Toast, ToastConformance.Toast) };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToastRoot<NoPayload> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToastRoot<NoPayload>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToastRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToastRoot<NoPayload>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToastRootState, string?>)(_ => classValue()));
    }
}

// ToastContent.test.tsx: describeConformance(<Toast.Content />, () => ({ refInstanceof: HTMLDivElement, render inside a root })). TST-STR-04, TST-STA-26.
/// <summary>The conformance suite on <see cref="ToastContent"/>.</summary>
public sealed class ToastContentConformanceTests : PartConformanceTests<ToastContent>
{
    public ToastContentConformanceTests()
    {
        ToastConformance.Prepare(Context);
        Context.RenderTree.Add<ToastProvider<NoPayload>>();
        Context.RenderTree.Add<ToastViewport<NoPayload>>();
        Context.RenderTree.Add<ToastRoot<NoPayload>>(p => p.Add(x => x.Toast, ToastConformance.Toast));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToastContent> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToastContent part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToastContent> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToastContentState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToastContent> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToastContentState, string?>)(_ => classValue()));
    }
}

// ToastTitle.test.tsx: describeConformance(<Toast.Title />, () => ({ refInstanceof: HTMLHeadingElement, render inside a root whose toast has a title })). TST-STR-05, TST-STA-26.
/// <summary>The conformance suite on <see cref="ToastTitle"/>.</summary>
public sealed class ToastTitleConformanceTests : PartConformanceTests<ToastTitle>
{
    public ToastTitleConformanceTests()
    {
        ToastConformance.Prepare(Context);
        Context.RenderTree.Add<ToastProvider<NoPayload>>();
        Context.RenderTree.Add<ToastViewport<NoPayload>>();
        Context.RenderTree.Add<ToastRoot<NoPayload>>(p => p.Add(x => x.Toast, ToastConformance.Toast));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToastTitle> Options { get; } = new("h2") { RendersAgainOnMount = true };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToastTitle part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToastTitle> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToastTitleState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToastTitle> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToastTitleState, string?>)(_ => classValue()));
    }
}

// ToastDescription.test.tsx: describeConformance(<Toast.Description />, () => ({ refInstanceof: HTMLParagraphElement, render inside a root whose toast has a description })). TST-STR-06, TST-STA-26.
/// <summary>The conformance suite on <see cref="ToastDescription"/>.</summary>
public sealed class ToastDescriptionConformanceTests : PartConformanceTests<ToastDescription>
{
    public ToastDescriptionConformanceTests()
    {
        ToastConformance.Prepare(Context);
        Context.RenderTree.Add<ToastProvider<NoPayload>>();
        Context.RenderTree.Add<ToastViewport<NoPayload>>();
        Context.RenderTree.Add<ToastRoot<NoPayload>>(p => p.Add(x => x.Toast, ToastConformance.Toast));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToastDescription> Options { get; } = new("p") { RendersAgainOnMount = true };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToastDescription part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToastDescription> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToastDescriptionState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToastDescription> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToastDescriptionState, string?>)(_ => classValue()));
    }
}

// ToastAction.test.tsx: describeConformance(<Toast.Action>action</Toast.Action>, () => ({ refInstanceof: HTMLButtonElement, testRenderPropWith: 'button', button: true, render inside a root })). TST-STR-07, TST-API-16, TST-DOM-18, TST-STA-26.
/// <summary>The conformance suite on <see cref="ToastAction"/>.</summary>
public sealed class ToastActionConformanceTests : PartConformanceTests<ToastAction>
{
    public ToastActionConformanceTests()
    {
        ToastConformance.Prepare(Context);
        Context.RenderTree.Add<ToastProvider<NoPayload>>();
        Context.RenderTree.Add<ToastViewport<NoPayload>>();
        Context.RenderTree.Add<ToastRoot<NoPayload>>(p => p.Add(x => x.Toast, ToastConformance.Toast));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToastAction> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button", Required = p => p.AddChildContent("action") };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToastAction part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToastAction> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToastActionState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToastAction> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToastActionState, string?>)(_ => classValue()));
    }
}

// ToastClose.test.tsx: describeConformance(<Toast.Close />, () => ({ refInstanceof: HTMLButtonElement, testRenderPropWith: 'button', button: true, render inside a root })). TST-STR-08, TST-API-16, TST-DOM-18, TST-STA-26.
/// <summary>The conformance suite on <see cref="ToastClose"/>.</summary>
public sealed class ToastCloseConformanceTests : PartConformanceTests<ToastClose>
{
    public ToastCloseConformanceTests()
    {
        ToastConformance.Prepare(Context);
        Context.RenderTree.Add<ToastProvider<NoPayload>>();
        Context.RenderTree.Add<ToastViewport<NoPayload>>();
        Context.RenderTree.Add<ToastRoot<NoPayload>>(p => p.Add(x => x.Toast, ToastConformance.Toast));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToastClose> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToastClose part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToastClose> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ToastCloseState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToastClose> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ToastCloseState, string?>)(_ => classValue()));
    }
}

// ToastPortal.test.tsx: describeConformance(<Toast.Portal />, () => ({ refInstanceof: HTMLDivElement })), rendered without a Provider (TST-STR-09). TST-DOM-20, TST-STA-26.
/// <summary>The conformance suite on <see cref="ToastPortal"/>.</summary>
public sealed class ToastPortalConformanceTests : PartConformanceTests<ToastPortal>
{
    public ToastPortalConformanceTests()
    {
        ToastConformance.Prepare(Context);

    }

    /// <inheritdoc />
    protected override ConformanceOptions<ToastPortal> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ToastPortal part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ToastPortal> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ToastPortal> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}
