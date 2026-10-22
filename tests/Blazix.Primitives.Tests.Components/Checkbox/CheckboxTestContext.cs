using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the Checkbox tests: the library's services, a captured logger, and bUnit's JS interop mock standing
/// in for the <c>button</c> module on the root, the <c>dom</c> module on the hidden input and the <c>transitions</c> module
/// on each indicator. A test drives the twins as the modules would (ADR 06 §1): <see cref="ToggleAsync"/> is the Server
/// report of a click on the input, <see cref="DecideToggleAsync"/> the WebAssembly decision inside it, and
/// <see cref="ReportTransitionAsync"/> and <see cref="CompleteExitAsync"/> the indicator's machine.
/// </summary>
public sealed class CheckboxTestContext : IDisposable
{
    public const string ButtonModulePath = "./_content/Blazix.Primitives/js/button.js";
    public const string DomModulePath = "./_content/Blazix.Primitives/js/dom.js";
    public const string TransitionsModulePath = "./_content/Blazix.Primitives/js/transitions.js";

    public CheckboxTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.Services.AddSingleton<ILogger<CheckboxRoot>>(Logger);
        ButtonModule = Context.JSInterop.SetupModule(ButtonModulePath);
        ButtonHandle = SetupHandle(ButtonModule);
        DomModule = Context.JSInterop.SetupModule(DomModulePath);
        DomHandle = SetupHandle(DomModule);
        TransitionsModule = Context.JSInterop.SetupModule(TransitionsModulePath);
        TransitionsHandle = SetupHandle(TransitionsModule);
    }

    public BunitContext Context { get; } = new();

    public CapturingLogger<CheckboxRoot> Logger { get; } = new();

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    public BunitJSModuleInterop DomModule { get; }

    public BunitJSModuleInterop DomHandle { get; }

    public BunitJSModuleInterop TransitionsModule { get; }

    public BunitJSModuleInterop TransitionsHandle { get; }

    /// <summary>The options JSON of every <c>dom</c> attach and update, in order.</summary>
    public IReadOnlyList<string> DomOptions =>
        [
            .. DomModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!),
            .. DomHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!),
        ];

    /// <summary>The options JSON of every <c>transitions</c> attach, in order.</summary>
    public IReadOnlyList<string> TransitionsAttaches => [.. TransitionsModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!)];

    /// <summary>The options JSON of every <c>transitions</c> update, in order.</summary>
    public IReadOnlyList<string> TransitionsUpdates => [.. TransitionsHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!)];

    /// <summary>The options JSON of the last <c>button</c> attach or update.</summary>
    public string LastButtonOptions =>
        ButtonHandle.Invocations["update"].Count > 0
            ? (string)ButtonHandle.Invocations["update"][^1].Arguments[0]!
            : (string)ButtonModule.Invocations["attach"][^1].Arguments[1]!;

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    /// <summary>The snapshot JSON of the click the hidden input receives, as the module takes it.</summary>
    public static string InputClickJson(bool shiftKey = false)
    {
        return $$"""
            {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":{{Bool(shiftKey)}},"pointerType":"","detail":0,"buttons":0,
             "target":{"id":null,"tagName":"INPUT"},"defaultPrevented":false,"timeStamp":3}
            """;
    }

    /// <summary>The snapshot JSON of a click on the root, as the module takes it for the activation decision.</summary>
    public static string RootClickJson()
    {
        return """
            {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":1,"buttons":0,
             "target":{"id":null,"tagName":"SPAN"},"defaultPrevented":false,"timeStamp":2}
            """;
    }

    /// <summary>The <see cref="ButtonTwin"/> attached at <paramref name="index"/>, the last by default.</summary>
    internal ButtonTwin ButtonTwin(int index = -1) =>
        ((DotNetObjectReference<ButtonTwin>)ButtonModule.Invocations["attach"][index < 0 ? ^1 : index].Arguments[2]!).Value;

    /// <summary>The <see cref="TransitionsTwin"/> attached last.</summary>
    internal TransitionsTwin TransitionsTwin() =>
        ((DotNetObjectReference<TransitionsTwin>)TransitionsModule.Invocations["attach"][^1].Arguments[2]!).Value;

    /// <summary>Reports a toggle of the hidden input as the module does on Server, with the input's new value.</summary>
    internal Task ToggleAsync<TComponent>(IRenderedComponent<TComponent> cut, bool @checked, bool shiftKey = false, int twin = -1)
        where TComponent : IComponent
    {
        ButtonTwin target = ButtonTwin(twin);
        return cut.InvokeAsync(() => target.OnToggle($$"""{"checked":{{Bool(@checked)}},"event":{{InputClickJson(shiftKey)}}}"""));
    }

    /// <summary>Asks the toggle decision as the module does on WebAssembly, inside the input's click.</summary>
    internal Task<string> DecideToggleAsync<TComponent>(IRenderedComponent<TComponent> cut, int twin = -1)
        where TComponent : IComponent
    {
        ButtonTwin target = ButtonTwin(twin);
        return cut.InvokeAsync(() => target.Decide(Blazix.Primitives.Interop.ButtonTwin.TogglePoint, InputClickJson()));
    }

    /// <summary>Asks the activation decision of a root click as the module does on WebAssembly.</summary>
    internal Task<string> DecideRootClickAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        ButtonTwin target = ButtonTwin();
        return cut.InvokeAsync(() => target.Decide(Blazix.Primitives.Interop.ButtonTwin.ActivatePoint, RootClickJson()));
    }

    /// <summary>Reports a transition status change of the last attached indicator, as the <c>transitions</c> module does.</summary>
    internal Task ReportTransitionAsync<TComponent>(IRenderedComponent<TComponent> cut, string? status, bool mounted)
        where TComponent : IComponent
    {
        TransitionsTwin twin = TransitionsTwin();
        return cut.InvokeAsync(() => twin.OnStatusChange(JsonSerializer.Serialize(new { status, mounted })));
    }

    /// <summary>Reports that the exit animations of the last attached indicator finished.</summary>
    internal Task CompleteExitAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        TransitionsTwin twin = TransitionsTwin();
        return cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}
