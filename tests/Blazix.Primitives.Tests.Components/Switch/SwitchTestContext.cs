using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the Switch tests: the library's services, a captured logger, and bUnit's JS interop mock standing
/// in for the <c>button</c> module on the root, the <c>dom</c> module on the hidden input and the <c>form</c> module on both.
/// A test drives the twins as the modules would (ADR 06 §1): <see cref="ToggleAsync"/> is the Server report of a click on
/// the input, <see cref="DecideToggleAsync"/> the WebAssembly decision inside it; <see cref="SettleFormAsync"/> answers the
/// hidden input's refresh and <see cref="BlurAsync"/> reports the root's blur (the Checkbox context's helpers).
/// </summary>
public sealed class SwitchTestContext : IDisposable
{
    public const string ButtonModulePath = "./_content/Blazix.Primitives/js/button.js";
    public const string DomModulePath = "./_content/Blazix.Primitives/js/dom.js";
    public const string FormModulePath = "./_content/Blazix.Primitives/js/form.js";
    public const string TransitionsModulePath = "./_content/Blazix.Primitives/js/transitions.js";

    private int answeredRevision;

    public SwitchTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.Services.AddSingleton<ILogger<SwitchRoot>>(Logger);
        ButtonModule = Context.JSInterop.SetupModule(ButtonModulePath);
        ButtonHandle = SetupHandle(ButtonModule);
        DomModule = Context.JSInterop.SetupModule(DomModulePath);
        DomHandle = SetupHandle(DomModule);
        FormModule = Context.JSInterop.SetupModule(FormModulePath);
        FormHandle = SetupHandle(FormModule);
        TransitionsModule = Context.JSInterop.SetupModule(TransitionsModulePath);
        TransitionsHandle = SetupHandle(TransitionsModule);
    }

    public BunitContext Context { get; } = new();

    public CapturingLogger<SwitchRoot> Logger { get; } = new();

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    public BunitJSModuleInterop DomModule { get; }

    public BunitJSModuleInterop DomHandle { get; }

    public BunitJSModuleInterop FormModule { get; }

    public BunitJSModuleInterop FormHandle { get; }

    public BunitJSModuleInterop TransitionsModule { get; }

    public BunitJSModuleInterop TransitionsHandle { get; }

    /// <summary>Gets or sets the native validity the hidden input reports on a refresh.</summary>
    internal NativeValidity InputValidity { get; set; } = NativeValidity.Default;

    /// <summary>The control options of every <c>form</c> attach and update, in order.</summary>
    internal IReadOnlyList<FormControlOptions> FormOptions =>
        [
            .. FormModule.Invocations["attach"].Select(i => Parse((string)i.Arguments[1]!)),
            .. FormHandle.Invocations["update"].Select(i => Parse((string)i.Arguments[0]!)),
        ];

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

    /// <summary>The <see cref="FormTwin"/> of the switch attached last (a field label's twin is skipped).</summary>
    internal FormTwin FormTwin() =>
        ((DotNetObjectReference<FormTwin>)FormModule.Invocations["attach"]
            .Last(i => JsonSerializer.Deserialize((string)i.Arguments[1]!, BlazixJsonContext.Default.FormOptions)!.Control is not null)
            .Arguments[2]!).Value;

    /// <summary>
    /// Answers the hidden input's latest refresh, as the module does: a <c>sync</c> report with its validity; then lets the
    /// work that waited for it run on the renderer's dispatcher.
    /// </summary>
    internal async Task SettleFormAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        FormControlOptions? input = FormOptions.LastOrDefault(o => !o.ReportEvents);
        if (input is null || input.Revision <= answeredRevision)
        {
            return;
        }

        answeredRevision = input.Revision;
        await ReportFormAsync(cut, "sync", null, input.Revision, input.CustomValidity is not null);
        await cut.InvokeAsync(static () => { });
    }

    /// <summary>Reports the hidden input's current native validity, as the module does when its state changes.</summary>
    internal async Task SyncInputAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        await ReportFormAsync(cut, "sync", null, answeredRevision, false);
        await cut.InvokeAsync(static () => { });
    }

    /// <summary>
    /// Ends every exit in progress as the transitions module reports it, with no animation to wait for: each live twin (a
    /// field error's) receives the completion of the close.
    /// </summary>
    internal async Task CompleteExitsAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        foreach (JSRuntimeInvocation invocation in TransitionsModule.Invocations["attach"])
        {
            TransitionsTwin twin;
            try
            {
                twin = ((DotNetObjectReference<TransitionsTwin>)invocation.Arguments[2]!).Value;
            }
            catch (ObjectDisposedException)
            {
                continue;
            }

            await cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
        }
    }

    /// <summary>Reports the root's blur as the module does, then answers the refresh it starts.</summary>
    internal async Task BlurAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        await ReportFormAsync(cut, "blur", new EventSnapshot { Kind = EventKind.FocusEvent, Type = "blur", Target = new EventTargetInfo { TagName = "SPAN" } }, 0, false);
        await SettleFormAsync(cut);
    }

    /// <summary>Reports the native label the hidden input's handle discovered, as the module does.</summary>
    internal Task ReportLabelAsync<TComponent>(IRenderedComponent<TComponent> cut, string? labelId)
        where TComponent : IComponent
    {
        FormTwin twin = FormTwin();
        string json = JsonSerializer.Serialize(new FormControlReport { Type = "label", Value = labelId }, BlazixJsonContext.Default.FormControlReport);
        return cut.InvokeAsync(() => twin.OnControlReport(json));
    }

    /// <summary>Reports that the root already had focus when the module attached to it or it became enabled.</summary>
    internal Task FocusedAtAttachAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        return ReportFormAsync(cut, "focus", null, 0, false);
    }

    private static FormControlOptions Parse(string json) =>
        JsonSerializer.Deserialize(json, BlazixJsonContext.Default.FormOptions)!.Control ?? new FormControlOptions();

    private Task ReportFormAsync<TComponent>(IRenderedComponent<TComponent> cut, string type, EventSnapshot? snapshot, int revision, bool ownMessage)
        where TComponent : IComponent
    {
        FormTwin twin = FormTwin();
        FormControlReport report = new()
        {
            Type = type,
            Validity = InputValidity with { OwnMessageInstalled = ownMessage },
            Event = snapshot,
            Revision = revision,
        };
        string json = JsonSerializer.Serialize(report, BlazixJsonContext.Default.FormControlReport);
        return cut.InvokeAsync(() => twin.OnControlReport(json));
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
