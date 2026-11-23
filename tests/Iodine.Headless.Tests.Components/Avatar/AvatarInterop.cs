using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The Avatar tests' bUnit context: the <c>dom</c> and <c>transitions</c> modules mocked as the twin contract tests mock
/// them, a <see cref="FakeTimeProvider"/> as the <see cref="TimeProvider"/>, and the module's side of the conversation:
/// the twins' <c>[JSInvokable]</c> callbacks driven the way the modules call them (ADR 06 §1: L2 drives the twin; the
/// modules themselves are L3 and L4).
/// </summary>
internal sealed class AvatarInterop : IDisposable
{
    public const string DomModulePath = "./_content/Blazix.Primitives/js/dom.js";
    public const string TransitionsModulePath = "./_content/Blazix.Primitives/js/transitions.js";

    public AvatarInterop()
    {
        Context.Services.AddBlazixPrimitives();
        Context.Services.AddSingleton<TimeProvider>(Clock);
        DomModule = Context.JSInterop.SetupModule(DomModulePath);
        DomHandle = SetupHandle(DomModule);
        TransitionsModule = Context.JSInterop.SetupModule(TransitionsModulePath);
        TransitionsHandle = SetupHandle(TransitionsModule);
    }

    public BunitContext Context { get; } = new();

    public FakeTimeProvider Clock { get; } = new();

    public BunitJSModuleInterop DomModule { get; }

    public BunitJSModuleInterop DomHandle { get; }

    public BunitJSModuleInterop TransitionsModule { get; }

    public BunitJSModuleInterop TransitionsHandle { get; }

    public IReadOnlyList<JSRuntimeInvocation> DomAttaches => Invocations(DomModule, "attach");

    public IReadOnlyList<JSRuntimeInvocation> DomUpdates => Invocations(DomHandle, "update");

    public IReadOnlyList<JSRuntimeInvocation> TransitionsAttaches => Invocations(TransitionsModule, "attach");

    public IReadOnlyList<JSRuntimeInvocation> TransitionsUpdates => Invocations(TransitionsHandle, "update");

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    public IRenderedComponent<AvatarHarness> Render(Action<ComponentParameterCollectionBuilder<AvatarHarness>>? parameters = null)
    {
        return Context.Render<AvatarHarness>(p => parameters?.Invoke(p));
    }

    /// <summary>Reports an image status the way the <c>dom</c> module does: <c>OnImageStatus</c> on the image's twin.</summary>
    public void ReportImage(IRenderedComponent<AvatarHarness> cut, string status)
    {
        DomTwin twin = Twin<DomTwin>(DomAttaches);
        cut.InvokeAsync(() => twin.OnImageStatus(JsonSerializer.Serialize(new { status })));
    }

    /// <summary>Reports a transition status change the way the <c>transitions</c> module does.</summary>
    public void ReportTransition(IRenderedComponent<AvatarHarness> cut, string? status, bool mounted)
    {
        TransitionsTwin twin = Twin<TransitionsTwin>(TransitionsAttaches);
        cut.InvokeAsync(() => twin.OnStatusChange(JsonSerializer.Serialize(new { status, mounted })));
    }

    /// <summary>Reports that the exit animations finished, the <c>transitions</c> module's completion message.</summary>
    public void CompleteExit(IRenderedComponent<AvatarHarness> cut)
    {
        TransitionsTwin twin = Twin<TransitionsTwin>(TransitionsAttaches);
        cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
    }

    public static T Twin<T>(IReadOnlyList<JSRuntimeInvocation> attaches)
        where T : class
    {
        Assert.NotEmpty(attaches);
        return Assert.IsType<DotNetObjectReference<T>>(attaches[^1].Arguments[2]).Value;
    }

    public static IReadOnlyList<JSRuntimeInvocation> Invocations(BunitJSModuleInterop module, string identifier)
    {
        return [.. module.Invocations[identifier]];
    }

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}
