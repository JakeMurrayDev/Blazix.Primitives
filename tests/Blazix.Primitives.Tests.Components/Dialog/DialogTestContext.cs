using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the Dialog tests: the library's services, captured loggers, and bUnit's JS interop mock standing in
/// for the six modules a dialog loads (<c>portal</c>, <c>transitions</c>, <c>button</c>, <c>interactions</c>,
/// <c>focus-manager</c>, <c>scroll-lock</c>), so a test reads what the parts sent and drives the twins' callbacks as the
/// modules would (ADR 06 §1).
/// </summary>
public sealed class DialogTestContext : IDisposable
{
    private const string Prefix = "./_content/Blazix.Primitives/js/";

    public DialogTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.Services.AddSingleton<ILogger<DialogRoot<NoPayload>>>(RootLogger);
        Context.Services.AddSingleton<ILogger<DialogPopup>>(PopupLogger);
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        PortalModule = Context.JSInterop.SetupModule(Prefix + "portal.js");
        PortalHandle = SetupHandle(PortalModule);
        TransitionsModule = Context.JSInterop.SetupModule(Prefix + "transitions.js");
        TransitionsHandle = SetupHandle(TransitionsModule);
        ButtonModule = Context.JSInterop.SetupModule(Prefix + "button.js");
        ButtonHandle = SetupHandle(ButtonModule);
        InteractionsModule = Context.JSInterop.SetupModule(Prefix + "interactions.js");
        InteractionsHandle = SetupHandle(InteractionsModule);
        FocusManagerModule = Context.JSInterop.SetupModule(Prefix + "focus-manager.js");
        FocusManagerHandle = SetupHandle(FocusManagerModule);
        ScrollLockModule = Context.JSInterop.SetupModule(Prefix + "scroll-lock.js");
        ScrollLockHandle = SetupHandle(ScrollLockModule);
        PopupModule = Context.JSInterop.SetupModule(Prefix + "popup.js");
        PopupHandle = SetupHandle(PopupModule);
    }

    public BunitContext Context { get; } = new();

    public CapturingLogger<DialogRoot<NoPayload>> RootLogger { get; } = new();

    public CapturingLogger<DialogPopup> PopupLogger { get; } = new();

    public BunitJSModuleInterop PortalModule { get; }

    public BunitJSModuleInterop PortalHandle { get; }

    public BunitJSModuleInterop TransitionsModule { get; }

    public BunitJSModuleInterop TransitionsHandle { get; }

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    public BunitJSModuleInterop InteractionsModule { get; }

    public BunitJSModuleInterop InteractionsHandle { get; }

    public BunitJSModuleInterop FocusManagerModule { get; }

    public BunitJSModuleInterop FocusManagerHandle { get; }

    public BunitJSModuleInterop ScrollLockModule { get; }

    public BunitJSModuleInterop ScrollLockHandle { get; }

    public BunitJSModuleInterop PopupModule { get; }

    public BunitJSModuleInterop PopupHandle { get; }

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    /// <summary>The options JSON of every attach of a module, in order.</summary>
    public static IReadOnlyList<string?> AttachOptions(BunitJSModuleInterop module) =>
        [.. module.Invocations["attach"].Select(call => call.Arguments[1] as string)];

    /// <summary>The options JSON of every update sent through a module's handles, in order.</summary>
    public static IReadOnlyList<string?> UpdateOptions(BunitJSModuleInterop handle) =>
        [.. handle.Invocations["update"].Select(call => call.Arguments[0] as string)];

    /// <summary>The twin the n-th attach of a module used.</summary>
    internal static TTwin TwinAt<TTwin>(BunitJSModuleInterop module, int index)
        where TTwin : class =>
        ((DotNetObjectReference<TTwin>)module.Invocations["attach"][index].Arguments[2]!).Value;

    /// <summary>The twin the last attach of a module used.</summary>
    internal static TTwin LastTwin<TTwin>(BunitJSModuleInterop module)
        where TTwin : class =>
        ((DotNetObjectReference<TTwin>)module.Invocations["attach"][^1].Arguments[2]!).Value;

    /// <summary>A click's snapshot as the <c>interactions</c> module sends it.</summary>
    public static string ClickJson(string pointerType = "mouse", int detail = 1) =>
        $$"""{"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,"metaKey":false,"shiftKey":false,"pointerType":"{{pointerType}}","detail":{{detail}},"buttons":0,"defaultPrevented":false,"timeStamp":3.5}""";

    /// <summary>An Escape keydown's snapshot as the <c>interactions</c> module sends it.</summary>
    public const string EscapeJson = """{"kind":"KeyboardEvent","type":"keydown","key":"Escape","code":"Escape","altKey":false,"ctrlKey":false,"metaKey":false,"shiftKey":false,"detail":0,"buttons":0,"defaultPrevented":false,"timeStamp":4.5}""";

    /// <summary>A pointerdown's snapshot as the <c>interactions</c> module sends it.</summary>
    public const string PointerDownJson = """{"kind":"PointerEvent","type":"pointerdown","key":null,"code":null,"altKey":false,"ctrlKey":false,"metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":1,"buttons":1,"defaultPrevented":false,"timeStamp":5.5}""";

    /// <summary>The <c>interactions</c> twin of the trigger whose options name <paramref name="triggerId"/>, its last attach.</summary>
    internal InteractionsTwin TriggerTwin(string triggerId) => InteractionsTwinWhere($"\"triggerId\":\"{triggerId}\"");

    /// <summary>The <c>interactions</c> twin of the root's session attached last (the token form).</summary>
    internal InteractionsTwin SessionTwin() => InteractionsTwinWhere("\"session\":");

    /// <summary>The <c>interactions</c> twin of the popup's floating handle attached last.</summary>
    internal InteractionsTwin FloatingTwin() => InteractionsTwinWhere("\"floating\":");

    /// <summary>Reports a press of a trigger as the module does on Server: the open state the press computed.</summary>
    public Task PressAsync<TComponent>(IRenderedComponent<TComponent> cut, string triggerId, bool open, string interactionType = "mouse")
        where TComponent : Microsoft.AspNetCore.Components.IComponent =>
        cut.InvokeAsync(() => TriggerTwin(triggerId).OnPress($$"""{"open":{{(open ? "true" : "false")}},"reason":"trigger-press","triggerId":"{{triggerId}}","interactionType":"{{interactionType}}","event":{{ClickJson(interactionType)}}}"""));

    /// <summary>Reports the session's Escape on WebAssembly: the decision the module asks inside the keydown, as its answer string.</summary>
    public Task<string> EscapeAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : Microsoft.AspNetCore.Components.IComponent =>
        cut.InvokeAsync(() => SessionTwin().Decide(InteractionsTwin.EscapePoint, EscapeJson));

    /// <summary>Reports a request of the session as the module does (an outside press, a Server Escape).</summary>
    public Task RequestAsync<TComponent>(IRenderedComponent<TComponent> cut, bool open, string reason, string eventJson)
        where TComponent : Microsoft.AspNetCore.Components.IComponent =>
        cut.InvokeAsync(() => SessionTwin().OnRequest($$"""{"open":{{(open ? "true" : "false")}},"reason":"{{reason}}","event":{{eventJson}},"triggerId":null}"""));

    /// <summary>Reports the popup's finished enter or exit as the <c>transitions</c> module does (its handle reports completion).</summary>
    public Task CompletePopupTransitionAsync<TComponent>(IRenderedComponent<TComponent> cut, bool open)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        IReadOnlyList<JSRuntimeInvocation> attaches = TransitionsModule.Invocations["attach"];
        for (int i = attaches.Count - 1; i >= 0; i--)
        {
            if (attaches[i].Arguments[1] is string options && options.Contains("\"reportOpenComplete\":true", StringComparison.Ordinal))
            {
                TransitionsTwin twin = ((DotNetObjectReference<TransitionsTwin>)attaches[i].Arguments[2]!).Value;
                return cut.InvokeAsync(() => twin.OnTransitionComplete(open ? """{"open":true}""" : """{"open":false}"""));
            }
        }

        throw new InvalidOperationException("No transitions handle attached that reports its completion.");
    }

    private InteractionsTwin InteractionsTwinWhere(string fragment)
    {
        IReadOnlyList<JSRuntimeInvocation> attaches = InteractionsModule.Invocations["attach"];
        for (int i = attaches.Count - 1; i >= 0; i--)
        {
            if (attaches[i].Arguments[1] is string options && options.Contains(fragment, StringComparison.Ordinal))
            {
                return ((DotNetObjectReference<InteractionsTwin>)attaches[i].Arguments[2]!).Value;
            }
        }

        throw new InvalidOperationException($"No interactions handle attached with {fragment}.");
    }

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}
