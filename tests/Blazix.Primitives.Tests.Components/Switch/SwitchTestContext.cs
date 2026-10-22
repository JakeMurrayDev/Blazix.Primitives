using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the Switch tests: the library's services, a captured logger, and bUnit's JS interop mock standing
/// in for the <c>button</c> module on the root and the <c>dom</c> module on the hidden input. A test drives the twins as
/// the modules would (ADR 06 §1): <see cref="ToggleAsync"/> is the Server report of a click on the input,
/// <see cref="DecideToggleAsync"/> the WebAssembly decision inside it.
/// </summary>
public sealed class SwitchTestContext : IDisposable
{
    public const string ButtonModulePath = "./_content/Blazix.Primitives/js/button.js";
    public const string DomModulePath = "./_content/Blazix.Primitives/js/dom.js";

    public SwitchTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.Services.AddSingleton<ILogger<SwitchRoot>>(Logger);
        ButtonModule = Context.JSInterop.SetupModule(ButtonModulePath);
        ButtonHandle = SetupHandle(ButtonModule);
        DomModule = Context.JSInterop.SetupModule(DomModulePath);
        DomHandle = SetupHandle(DomModule);
    }

    public BunitContext Context { get; } = new();

    public CapturingLogger<SwitchRoot> Logger { get; } = new();

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    public BunitJSModuleInterop DomModule { get; }

    public BunitJSModuleInterop DomHandle { get; }

    /// <summary>The options JSON of every <c>dom</c> attach and update, in order.</summary>
    public IReadOnlyList<string> DomOptions =>
        [
            .. DomModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!),
            .. DomHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!),
        ];

    /// <summary>The options JSON of the last <c>button</c> attach or update.</summary>
    public string LastButtonOptions =>
        DomHandleOrAttach(ButtonModule, ButtonHandle);

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    /// <summary>The snapshot JSON of the click the hidden input receives, as the module takes it.</summary>
    public static string InputClickJson(bool shiftKey = false, bool ctrlKey = false)
    {
        return $$"""
            {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":{{Bool(ctrlKey)}},
             "metaKey":false,"shiftKey":{{Bool(shiftKey)}},"pointerType":"","detail":0,"buttons":0,
             "target":{"id":null,"tagName":"INPUT"},"defaultPrevented":false,"timeStamp":3}
            """;
    }

    /// <summary>The snapshot JSON of a click on the root, as the module takes it for the activation decision.</summary>
    public static string RootClickJson(bool shiftKey = false)
    {
        return $$"""
            {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":{{Bool(shiftKey)}},"pointerType":"mouse","detail":1,"buttons":0,
             "target":{"id":null,"tagName":"SPAN"},"defaultPrevented":false,"timeStamp":2}
            """;
    }

    /// <summary>The <see cref="ButtonTwin"/> attached last.</summary>
    internal ButtonTwin ButtonTwin() =>
        ((DotNetObjectReference<ButtonTwin>)ButtonModule.Invocations["attach"][^1].Arguments[2]!).Value;

    /// <summary>Reports a toggle of the hidden input as the module does on Server, with the input's new value.</summary>
    internal Task ToggleAsync<TComponent>(IRenderedComponent<TComponent> cut, bool @checked, bool shiftKey = false)
        where TComponent : IComponent
    {
        ButtonTwin twin = ButtonTwin();
        return cut.InvokeAsync(() => twin.OnToggle($$"""{"checked":{{Bool(@checked)}},"event":{{InputClickJson(shiftKey)}}}"""));
    }

    /// <summary>Asks the toggle decision as the module does on WebAssembly, inside the input's click.</summary>
    internal Task<string> DecideToggleAsync<TComponent>(IRenderedComponent<TComponent> cut, bool shiftKey = false)
        where TComponent : IComponent
    {
        ButtonTwin twin = ButtonTwin();
        return cut.InvokeAsync(() => twin.Decide(Blazix.Primitives.Interop.ButtonTwin.TogglePoint, InputClickJson(shiftKey)));
    }

    /// <summary>Asks the activation decision of a root click as the module does on WebAssembly.</summary>
    internal Task<string> DecideRootClickAsync<TComponent>(IRenderedComponent<TComponent> cut, bool shiftKey = false)
        where TComponent : IComponent
    {
        ButtonTwin twin = ButtonTwin();
        return cut.InvokeAsync(() => twin.Decide(Blazix.Primitives.Interop.ButtonTwin.ActivatePoint, RootClickJson(shiftKey)));
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static string DomHandleOrAttach(BunitJSModuleInterop module, BunitJSModuleInterop handle)
    {
        return handle.Invocations["update"].Count > 0
            ? (string)handle.Invocations["update"][^1].Arguments[0]!
            : (string)module.Invocations["attach"][^1].Arguments[1]!;
    }

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}
