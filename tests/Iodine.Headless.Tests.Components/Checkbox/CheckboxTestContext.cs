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
/// <see cref="ReportTransitionAsync"/> and <see cref="CompleteExitAsync"/> the indicator's machine. Inside a field the
/// <c>form</c> module stands for the root's handle (its blur and focus reports, <see cref="BlurAsync"/>) and the hidden
/// input's validation handle, whose refreshes <see cref="SettleFormAsync"/> answers with <see cref="InputValidity"/>.
/// </summary>
public sealed class CheckboxTestContext : IDisposable
{
    public const string ButtonModulePath = "./_content/Blazix.Primitives/js/button.js";
    public const string DomModulePath = "./_content/Blazix.Primitives/js/dom.js";
    public const string TransitionsModulePath = "./_content/Blazix.Primitives/js/transitions.js";
    public const string FormModulePath = "./_content/Blazix.Primitives/js/form.js";

    private int answeredRevision;

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
        FormModule = Context.JSInterop.SetupModule(FormModulePath);
        FormHandle = SetupHandle(FormModule);
    }

    public BunitContext Context { get; } = new();

    public CapturingLogger<CheckboxRoot> Logger { get; } = new();

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    public BunitJSModuleInterop DomModule { get; }

    public BunitJSModuleInterop DomHandle { get; }

    public BunitJSModuleInterop TransitionsModule { get; }

    public BunitJSModuleInterop TransitionsHandle { get; }

    public BunitJSModuleInterop FormModule { get; }

    public BunitJSModuleInterop FormHandle { get; }

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

    /// <summary>The <see cref="FormTwin"/> of the box attached last (a field label's twin is skipped).</summary>
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
        await ReportFormAsync(cut, FormTwin(), "sync", null, input.Revision, input.CustomValidity is not null);
        await cut.InvokeAsync(static () => { });
    }

    /// <summary>Reports the root's blur as the module does, then answers the refresh it starts.</summary>
    internal async Task BlurAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        await ReportFormAsync(cut, FormTwin(), "blur", BlurSnapshot(), 0, false);
        await SettleFormAsync(cut);
    }

    /// <summary>Reports the blur of one box of a group as the module does, then answers every refresh it starts.</summary>
    internal async Task BlurAsync<TComponent>(IRenderedComponent<TComponent> cut, CheckboxRoot box)
        where TComponent : IComponent
    {
        FormTwin twin = ((DotNetObjectReference<FormTwin>)FormModule.Invocations["attach"]
            .Last(i => Equals(i.Arguments[0], box.Element))
            .Arguments[2]!).Value;
        await ReportFormAsync(cut, twin, "blur", BlurSnapshot(), 0, false);
        await SettleAllFormsAsync(cut);
    }

    /// <summary>
    /// Answers the pending refresh of every hidden input with <see cref="InputValidity"/>, as the module does for each box
    /// of a group; twice, for the refreshes the work that waited for the first answers starts.
    /// </summary>
    internal async Task SettleAllFormsAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        for (int round = 0; round < 2; round++)
        {
            foreach (JSRuntimeInvocation attach in FormModule.Invocations["attach"].ToArray())
            {
                if (Parse((string)attach.Arguments[1]!).ReportEvents)
                {
                    continue;
                }

                FormTwin twin = ((DotNetObjectReference<FormTwin>)attach.Arguments[2]!).Value;
                await ReportFormAsync(cut, twin, "sync", null, int.MaxValue, false);
            }

            await cut.InvokeAsync(static () => { });
        }
    }

    /// <summary>
    /// Reports a native submission of the enclosing <see cref="Form"/>, as the module does: the state it read from every
    /// enrolled control, here every hidden input with <paramref name="validity"/> (the roots read no constraint).
    /// </summary>
    internal async Task<FormSubmitAnswer> SubmitAsync<TComponent>(IRenderedComponent<TComponent> cut, Func<string, NativeValidity> validity)
        where TComponent : IComponent
    {
        JSRuntimeInvocation attach = FormModule.Invocations["attach"]
            .Last(i => JsonSerializer.Deserialize((string)i.Arguments[1]!, BlazixJsonContext.Default.FormOptions)!.Submit is not null);
        FormTwin twin = ((DotNetObjectReference<FormTwin>)attach.Arguments[2]!).Value;
        FormControlState[] controls =
        [
            .. cut.FindAll("input[type=checkbox][id]").Select(input => new FormControlState
            {
                Id = input.GetAttribute("id")!,
                Value = "on",
                Validity = validity(input.GetAttribute("id")!),
            }),
        ];
        FormSubmitRequest request = new()
        {
            Event = new EventSnapshot { Kind = EventKind.Event, Type = "submit", DefaultPrevented = true, Target = new EventTargetInfo { TagName = "FORM" } },
            Controls = controls,
        };
        string json = JsonSerializer.Serialize(request, BlazixJsonContext.Default.FormSubmitRequest);
        string answer = await cut.InvokeAsync(() => twin.OnSubmitRequest(json));
        return JsonSerializer.Deserialize(answer, BlazixJsonContext.Default.FormSubmitAnswer)!;
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
        return ReportFormAsync(cut, FormTwin(), "focus", null, 0, false);
    }

    private static FormControlOptions Parse(string json) =>
        JsonSerializer.Deserialize(json, BlazixJsonContext.Default.FormOptions)!.Control ?? new FormControlOptions();

    private static EventSnapshot BlurSnapshot() =>
        new() { Kind = EventKind.FocusEvent, Type = "blur", Target = new EventTargetInfo { TagName = "SPAN" } };

    private Task ReportFormAsync<TComponent>(IRenderedComponent<TComponent> cut, FormTwin twin, string type, EventSnapshot? snapshot, int revision, bool ownMessage)
        where TComponent : IComponent
    {
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

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}
