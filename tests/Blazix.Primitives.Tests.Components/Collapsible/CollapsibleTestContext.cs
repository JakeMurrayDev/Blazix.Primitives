using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the Collapsible tests: the library's services, captured loggers, and bUnit's JS interop mock
/// standing in for the <c>transitions</c>, <c>collapsible</c> and <c>button</c> modules, so a test can read what the parts
/// sent and drive the twins' callbacks as the modules would (ADR 06 §1, twin contract tests).
/// </summary>
public sealed class CollapsibleTestContext : IDisposable
{
    public const string TransitionsModulePath = "./_content/Blazix.Primitives/js/transitions.js";
    public const string CollapsibleModulePath = "./_content/Blazix.Primitives/js/collapsible.js";
    public const string ButtonModulePath = "./_content/Blazix.Primitives/js/button.js";

    public CollapsibleTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.Services.AddSingleton<ILogger<CollapsibleRoot>>(RootLogger);
        Context.Services.AddSingleton<ILogger<CollapsiblePanel>>(PanelLogger);
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        TransitionsModule = Context.JSInterop.SetupModule(TransitionsModulePath);
        TransitionsHandle = SetupHandle(TransitionsModule);
        CollapsibleModule = Context.JSInterop.SetupModule(CollapsibleModulePath);
        CollapsibleHandle = SetupHandle(CollapsibleModule);
        ButtonModule = Context.JSInterop.SetupModule(ButtonModulePath);
        ButtonHandle = SetupHandle(ButtonModule);
    }

    public BunitContext Context { get; } = new();

    public CapturingLogger<CollapsibleRoot> RootLogger { get; } = new();

    public CapturingLogger<CollapsiblePanel> PanelLogger { get; } = new();

    public BunitJSModuleInterop TransitionsModule { get; }

    public BunitJSModuleInterop TransitionsHandle { get; }

    public BunitJSModuleInterop CollapsibleModule { get; }

    public BunitJSModuleInterop CollapsibleHandle { get; }

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    /// <summary>The <see cref="CollapsibleTwin"/> of the panel attached last.</summary>
    internal CollapsibleTwin PanelTwin() => TwinOf<CollapsibleTwin>(CollapsibleModule);

    /// <summary>The <see cref="TransitionsTwin"/> the n-th attach used (the root attaches before its triggers).</summary>
    internal TransitionsTwin TransitionsTwinAt(int index) =>
        ((DotNetObjectReference<TransitionsTwin>)TransitionsModule.Invocations["attach"][index].Arguments[2]!).Value;

    /// <summary>The options JSON of every attach of a module, in order.</summary>
    public IReadOnlyList<string?> AttachOptions(BunitJSModuleInterop module) =>
        [.. module.Invocations["attach"].Select(invocation => invocation.Arguments[1] as string)];

    /// <summary>The options JSON of every update sent through a module's handles, in order.</summary>
    public IReadOnlyList<string?> UpdateOptions(BunitJSModuleInterop handle) =>
        [.. handle.Invocations["update"].Select(invocation => invocation.Arguments[0] as string)];

    /// <summary>Ends the panel's exit as the module reports it (the convergent hide and the completion message).</summary>
    public void CompletePanelExit<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        cut.InvokeAsync(() => PanelTwin().OnTransitionComplete("""{"open":false}"""));
    }

    private static TTwin TwinOf<TTwin>(BunitJSModuleInterop module)
        where TTwin : class =>
        ((DotNetObjectReference<TTwin>)module.Invocations["attach"][^1].Arguments[2]!).Value;

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}
