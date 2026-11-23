using System.Text.Json;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the Radio and RadioGroup tests: the library's services and bUnit's JS interop mock standing in for
/// the <c>button</c> module on each radio's root, the <c>dom</c> module on its hidden input, the <c>transitions</c> module
/// on each indicator, the <c>form</c> module on the root and the input inside a field, and the <c>composite</c> module on
/// the group. A test drives the twins as the modules would (ADR 06 §1): <see cref="ToggleAsync"/> is the Server report of
/// a click on a radio's input and <see cref="DecideToggleAsync"/> the WebAssembly decision inside it;
/// <see cref="FocusLeaveAsync"/> is the composite's report of focus leaving the group and <see cref="DecideSelectAsync"/>
/// its focus-driven selection decision; <see cref="ReportTransitionAsync"/> and <see cref="CompleteExitAsync"/> are the
/// indicator's machine; <see cref="SettleAllFormsAsync"/> answers every hidden input's refresh.
/// </summary>
public sealed class RadioTestContext : IDisposable
{
    public const string ButtonModulePath = "./_content/Blazix.Primitives/js/button.js";
    public const string DomModulePath = "./_content/Blazix.Primitives/js/dom.js";
    public const string TransitionsModulePath = "./_content/Blazix.Primitives/js/transitions.js";
    public const string FormModulePath = "./_content/Blazix.Primitives/js/form.js";
    public const string CompositeModulePath = "./_content/Blazix.Primitives/js/composite.js";

    public RadioTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        ButtonModule = Context.JSInterop.SetupModule(ButtonModulePath);
        ButtonHandle = SetupHandle(ButtonModule);
        DomModule = Context.JSInterop.SetupModule(DomModulePath);
        DomHandle = SetupHandle(DomModule);
        TransitionsModule = Context.JSInterop.SetupModule(TransitionsModulePath);
        TransitionsHandle = SetupHandle(TransitionsModule);
        FormModule = Context.JSInterop.SetupModule(FormModulePath);
        FormHandle = SetupHandle(FormModule);
        CompositeModule = Context.JSInterop.SetupModule(CompositeModulePath);
        CompositeHandle = SetupHandle(CompositeModule);
    }

    public BunitContext Context { get; } = new();

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    public BunitJSModuleInterop DomModule { get; }

    public BunitJSModuleInterop DomHandle { get; }

    public BunitJSModuleInterop TransitionsModule { get; }

    public BunitJSModuleInterop TransitionsHandle { get; }

    public BunitJSModuleInterop FormModule { get; }

    public BunitJSModuleInterop FormHandle { get; }

    public BunitJSModuleInterop CompositeModule { get; }

    public BunitJSModuleInterop CompositeHandle { get; }

    /// <summary>Gets or sets the native validity every hidden input reports on a refresh.</summary>
    internal NativeValidity InputValidity { get; set; } = NativeValidity.Default;

    /// <summary>Gets or sets the native validity of individual hidden inputs, by element id, over <see cref="InputValidity"/>.</summary>
    internal Dictionary<string, NativeValidity> InputValidityById { get; } = [];

    /// <summary>The options JSON of every <c>dom</c> attach and update, in order.</summary>
    public IReadOnlyList<string> DomOptions =>
        [
            .. DomModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!),
            .. DomHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!),
        ];

    /// <summary>The options JSON of every <c>dom</c> update, in order.</summary>
    public IReadOnlyList<string> DomUpdates => [.. DomHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!)];

    /// <summary>The options JSON of every <c>transitions</c> attach, in order.</summary>
    public IReadOnlyList<string> TransitionsAttaches => [.. TransitionsModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!)];

    /// <summary>The options JSON of every <c>transitions</c> update, in order.</summary>
    public IReadOnlyList<string> TransitionsUpdates => [.. TransitionsHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!)];

    /// <summary>The options JSON of every <c>composite</c> attach, in order.</summary>
    public IReadOnlyList<string> CompositeAttaches => [.. CompositeModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!)];

    /// <summary>The options JSON of every <c>composite</c> update, in order.</summary>
    public IReadOnlyList<string> CompositeUpdates => [.. CompositeHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!)];

    /// <summary>The options JSON of the composite's last attach or update.</summary>
    public string LastCompositeOptions => CompositeUpdates.Count > 0 ? CompositeUpdates[^1] : CompositeAttaches[^1];

    /// <summary>The options JSON of the <c>button</c> attach at <paramref name="index"/>, the last by default.</summary>
    public string ButtonAttachOptions(int index = -1) => (string)ButtonModule.Invocations["attach"][index < 0 ? ^1 : index].Arguments[1]!;

    /// <summary>The control options of every <c>form</c> attach and update, in order.</summary>
    internal IReadOnlyList<FormControlOptions> FormOptions =>
        [
            .. FormModule.Invocations["attach"].Select(i => ParseControl((string)i.Arguments[1]!)),
            .. FormHandle.Invocations["update"].Select(i => ParseControl((string)i.Arguments[0]!)),
        ];

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    /// <summary>The snapshot JSON of the click a hidden input receives, as the module takes it.</summary>
    public static string InputClickJson(bool shiftKey = false)
    {
        return $$"""
            {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":{{Bool(shiftKey)}},"pointerType":"","detail":0,"buttons":0,
             "target":{"id":null,"tagName":"INPUT"},"defaultPrevented":false,"timeStamp":3}
            """;
    }

    /// <summary>The snapshot JSON of a click on a radio's root, as the module takes it for the activation decision.</summary>
    public static string RootClickJson()
    {
        return """
            {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":1,"buttons":0,
             "target":{"id":null,"tagName":"SPAN"},"defaultPrevented":false,"timeStamp":2}
            """;
    }

    /// <summary>The snapshot JSON of a focus a radio received, as the composite module takes it for the selection decision.</summary>
    public static string FocusInJson(string targetId)
    {
        return $$"""
            {"kind":"FocusEvent","type":"focusin","key":null,"code":null,"altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
             "target":{"id":"{{targetId}}","tagName":"SPAN"},"defaultPrevented":false,"timeStamp":4}
            """;
    }

    /// <summary>The snapshot JSON of a keydown, as the composite module takes it for the navigation decision.</summary>
    public static string KeyDownJson(string key, string targetId)
    {
        return $$"""
            {"kind":"KeyboardEvent","type":"keydown","key":"{{key}}","code":"{{key}}","altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
             "target":{"id":"{{targetId}}","tagName":"SPAN"},"defaultPrevented":false,"timeStamp":5}
            """;
    }

    /// <summary>The ids of the hidden radio inputs' sibling roots, in document order.</summary>
    public static IReadOnlyList<IElement> Radios<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
        => cut.FindAll("[role=radio]");

    /// <summary>The hidden radio inputs, in document order.</summary>
    public static IReadOnlyList<IElement> Inputs<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
        => cut.FindAll("input[type=radio]");

    /// <summary>The <see cref="ButtonTwin"/> attached at <paramref name="index"/>, the last by default; radios attach in render order.</summary>
    internal ButtonTwin ButtonTwin(int index = -1) =>
        ((DotNetObjectReference<ButtonTwin>)ButtonModule.Invocations["attach"][index < 0 ? ^1 : index].Arguments[2]!).Value;

    /// <summary>The <see cref="CompositeTwin"/> attached last.</summary>
    internal CompositeTwin CompositeTwin() =>
        ((DotNetObjectReference<CompositeTwin>)CompositeModule.Invocations["attach"][^1].Arguments[2]!).Value;

    /// <summary>The <see cref="TransitionsTwin"/> attached last.</summary>
    internal TransitionsTwin TransitionsTwin() =>
        ((DotNetObjectReference<TransitionsTwin>)TransitionsModule.Invocations["attach"][^1].Arguments[2]!).Value;

    /// <summary>Reports a click on radio <paramref name="radio"/>'s hidden input as the module does on Server: the input's new <c>checked</c>, always true.</summary>
    internal Task ToggleAsync<TComponent>(IRenderedComponent<TComponent> cut, int radio = -1, bool shiftKey = false)
        where TComponent : IComponent
    {
        ButtonTwin target = ButtonTwin(radio);
        return cut.InvokeAsync(() => target.OnToggle($$"""{"checked":true,"event":{{InputClickJson(shiftKey)}}}"""));
    }

    /// <summary>Asks radio <paramref name="radio"/>'s toggle decision as the module does on WebAssembly, inside the input's click.</summary>
    internal Task<string> DecideToggleAsync<TComponent>(IRenderedComponent<TComponent> cut, int radio = -1)
        where TComponent : IComponent
    {
        ButtonTwin target = ButtonTwin(radio);
        return cut.InvokeAsync(() => target.Decide(Blazix.Primitives.Interop.ButtonTwin.TogglePoint, InputClickJson()));
    }

    /// <summary>Asks radio <paramref name="radio"/>'s activation decision of a root click as the module does on WebAssembly.</summary>
    internal Task<string> DecideRootClickAsync<TComponent>(IRenderedComponent<TComponent> cut, int radio = -1)
        where TComponent : IComponent
    {
        ButtonTwin target = ButtonTwin(radio);
        return cut.InvokeAsync(() => target.Decide(Blazix.Primitives.Interop.ButtonTwin.ActivatePoint, RootClickJson()));
    }

    /// <summary>Asks the group's focus-driven selection decision for the radio with the given element id, as the module does on WebAssembly.</summary>
    internal Task<string> DecideSelectAsync<TComponent>(IRenderedComponent<TComponent> cut, string radioId)
        where TComponent : IComponent
    {
        CompositeTwin twin = CompositeTwin();
        return cut.InvokeAsync(() => twin.Decide(Blazix.Primitives.Interop.CompositeTwin.SelectPoint, FocusInJson(radioId)));
    }

    /// <summary>Asks the group's navigation decision for a key on the radio with the given element id, as the module does on WebAssembly.</summary>
    internal Task<string> DecideNavigateAsync<TComponent>(IRenderedComponent<TComponent> cut, string key, string radioId)
        where TComponent : IComponent
    {
        CompositeTwin twin = CompositeTwin();
        return cut.InvokeAsync(() => twin.Decide(Blazix.Primitives.Interop.CompositeTwin.NavigatePoint, KeyDownJson(key, radioId)));
    }

    /// <summary>Reports that focus left the group, as the composite module does with <c>reportFocusLeave</c>, then answers every refresh it starts.</summary>
    internal async Task FocusLeaveAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        CompositeTwin twin = CompositeTwin();
        string json = """{"event":{"kind":"FocusEvent","type":"focusout","target":{"id":null,"tagName":"SPAN"},"defaultPrevented":false}}""";
        await cut.InvokeAsync(() => twin.OnFocusLeave(json));
        await SettleAllFormsAsync(cut);
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

    /// <summary>Reports that the exit of every element a transitions twin attached to finished, as the module does (a field error's).</summary>
    internal async Task CompleteExitsAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        foreach (JSRuntimeInvocation attach in TransitionsModule.Invocations["attach"].ToArray())
        {
            TransitionsTwin twin = ((DotNetObjectReference<TransitionsTwin>)attach.Arguments[2]!).Value;
            await cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
        }
    }

    /// <summary>The <see cref="FormTwin"/> attached to the given element (a radio's root or hidden input).</summary>
    internal FormTwin FormTwinOf(ElementReference? element) =>
        ((DotNetObjectReference<FormTwin>)FormModule.Invocations["attach"]
            .Last(i => i.Arguments[0] is ElementReference attached && string.Equals(attached.Id, element?.Id, StringComparison.Ordinal))
            .Arguments[2]!).Value;

    /// <summary>Reports a radio root's blur as the module does, then answers every refresh it starts.</summary>
    internal async Task BlurAsync<TComponent>(IRenderedComponent<TComponent> cut, ElementReference? root)
        where TComponent : IComponent
    {
        await ReportFormAsync(cut, FormTwinOf(root), "blur", new EventSnapshot { Kind = EventKind.FocusEvent, Type = "blur", Target = new EventTargetInfo { TagName = "SPAN" } }, 0, NativeValidity.Default);
        await SettleAllFormsAsync(cut);
    }

    /// <summary>Reports that a radio root already had focus when the module attached to it or it became enabled.</summary>
    internal Task FocusedAtAttachAsync<TComponent>(IRenderedComponent<TComponent> cut, ElementReference? root)
        where TComponent : IComponent
    {
        return ReportFormAsync(cut, FormTwinOf(root), "focus", null, 0, NativeValidity.Default);
    }

    /// <summary>Reports the native label a hidden input's handle discovered, as the module does.</summary>
    internal Task ReportLabelAsync<TComponent>(IRenderedComponent<TComponent> cut, ElementReference? input, string? labelId)
        where TComponent : IComponent
    {
        FormTwin twin = FormTwinOf(input);
        string json = JsonSerializer.Serialize(new FormControlReport { Type = "label", Value = labelId }, BlazixJsonContext.Default.FormControlReport);
        return cut.InvokeAsync(() => twin.OnControlReport(json));
    }

    /// <summary>
    /// Answers the pending refresh of every hidden input with its validity, as the module does for each radio of a group;
    /// twice, for the refreshes the work that waited for the first answers starts.
    /// </summary>
    internal async Task SettleAllFormsAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        for (int round = 0; round < 2; round++)
        {
            foreach (JSRuntimeInvocation attach in FormModule.Invocations["attach"].ToArray())
            {
                FormControlOptions options = ParseControl((string)attach.Arguments[1]!);
                if (options.ReportEvents || attach.Arguments[0] is not ElementReference element)
                {
                    continue;
                }

                FormTwin twin = ((DotNetObjectReference<FormTwin>)attach.Arguments[2]!).Value;
                await ReportFormAsync(cut, twin, "sync", null, int.MaxValue, ValidityOf(cut, element));
            }

            await cut.InvokeAsync(static () => { });
        }
    }

    /// <summary>
    /// Reports a native submission of the enclosing <see cref="Form"/>, as the module does: the state it read from every
    /// enrolled control, here every hidden radio input with an id and its validity.
    /// </summary>
    internal async Task<FormSubmitAnswer> SubmitAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        JSRuntimeInvocation attach = FormModule.Invocations["attach"]
            .Last(i => JsonSerializer.Deserialize((string)i.Arguments[1]!, BlazixJsonContext.Default.FormOptions)!.Submit is not null);
        FormTwin twin = ((DotNetObjectReference<FormTwin>)attach.Arguments[2]!).Value;
        FormControlState[] controls =
        [
            .. cut.FindAll("input[type=radio][id]").Select(input => new FormControlState
            {
                Id = input.GetAttribute("id")!,
                Value = input.GetAttribute("value") ?? "on",
                Validity = Eligibility(input, InputValidityById.TryGetValue(input.GetAttribute("id")!, out NativeValidity? own) ? own : InputValidity),
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

    /// <summary>Builds radios of type <see cref="string"/> with the given values, each with <c>data-testid</c> set to its value.</summary>
    public static RenderFragment StringRadios(params string[] values) => builder =>
    {
        foreach (string value in values)
        {
            builder.OpenComponent<RadioRoot<string>>(0);
            builder.SetKey(value);
            builder.AddComponentParameter(1, nameof(RadioRoot<string>.Value), value);
            builder.AddAttribute(2, "data-testid", value);
            builder.CloseComponent();
        }
    };

    private static FormControlOptions ParseControl(string json) =>
        JsonSerializer.Deserialize(json, BlazixJsonContext.Default.FormOptions)!.Control ?? new FormControlOptions();

    // The module reads `:disabled` with the validity, so a disabled input is never eligible (FFI-FRM-23).
    private static NativeValidity Eligibility(IElement input, NativeValidity validity) =>
        input.HasAttribute("disabled") ? validity with { Eligible = false } : validity;

    private NativeValidity ValidityOf<TComponent>(IRenderedComponent<TComponent> cut, ElementReference element)
        where TComponent : IComponent
    {
        IElement? input = cut.FindAll("input[type=radio]").FirstOrDefault(i => string.Equals(i.GetAttribute("blazor:elementReference"), element.Id, StringComparison.Ordinal));
        if (input is null)
        {
            return InputValidity;
        }

        return Eligibility(input, input.Id is { } id && InputValidityById.TryGetValue(id, out NativeValidity? own) ? own : InputValidity);
    }

    private static Task ReportFormAsync<TComponent>(IRenderedComponent<TComponent> cut, FormTwin twin, string type, EventSnapshot? snapshot, int revision, NativeValidity validity)
        where TComponent : IComponent
    {
        FormControlReport report = new()
        {
            Type = type,
            Validity = validity,
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
