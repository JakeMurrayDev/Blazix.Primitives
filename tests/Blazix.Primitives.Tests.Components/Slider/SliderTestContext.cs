using System.Globalization;
using System.Text.Json;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the Slider tests: the library's services and bUnit's JS interop mock standing in for the
/// <c>slider</c> module on the root, the <c>composite</c> module's registration-only list of thumbs and the <c>form</c>
/// module on a label. A test drives the twin as the module would (ADR 06 §1): <see cref="RequestAsync"/> is the module's
/// value report, <see cref="GestureAsync"/> its gesture report, <see cref="FocusAsync"/> its focus classification, and
/// <see cref="ReportThumbsAsync"/> the registry's order report; <see cref="SubmitAsync"/> is the <c>form</c> module's submit
/// request and <see cref="CompleteExitAsync"/> the <c>transitions</c> module's end of a field error's exit.
/// </summary>
public sealed class SliderTestContext : IDisposable
{
    public const string SliderModulePath = "./_content/Blazix.Primitives/js/slider.js";
    public const string CompositeModulePath = "./_content/Blazix.Primitives/js/composite.js";
    public const string FormModulePath = "./_content/Blazix.Primitives/js/form.js";
    public const string TransitionsModulePath = "./_content/Blazix.Primitives/js/transitions.js";

    public static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private int revision;

    public SliderTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        SliderModule = Context.JSInterop.SetupModule(SliderModulePath);
        SliderHandle = SetupHandle(SliderModule);
        CompositeModule = Context.JSInterop.SetupModule(CompositeModulePath);
        CompositeHandle = SetupHandle(CompositeModule);
        FormModule = Context.JSInterop.SetupModule(FormModulePath);
        FormHandle = SetupHandle(FormModule);
        TransitionsModule = Context.JSInterop.SetupModule(TransitionsModulePath);
        TransitionsHandle = SetupHandle(TransitionsModule);
    }

    public BunitContext Context { get; } = new();

    public BunitJSModuleInterop SliderModule { get; }

    public BunitJSModuleInterop SliderHandle { get; }

    public BunitJSModuleInterop CompositeModule { get; }

    public BunitJSModuleInterop CompositeHandle { get; }

    public BunitJSModuleInterop FormModule { get; }

    public BunitJSModuleInterop FormHandle { get; }

    public BunitJSModuleInterop TransitionsModule { get; }

    public BunitJSModuleInterop TransitionsHandle { get; }

    /// <summary>The options JSON of every <c>slider</c> attach, in order.</summary>
    public IReadOnlyList<string> SliderAttaches => [.. SliderModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!)];

    /// <summary>The options JSON of every <c>slider</c> update, in order.</summary>
    public IReadOnlyList<string> SliderUpdates => [.. SliderHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!)];

    /// <summary>The options of the last <c>slider</c> attach or update.</summary>
    public JsonElement LastOptions => JsonDocument.Parse(SliderUpdates.Count > 0 ? SliderUpdates[^1] : SliderAttaches[^1]).RootElement;

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    /// <summary>The snapshot JSON of a key on a thumb input.</summary>
    public static string KeyJson(string key = "ArrowRight", string? targetId = null, bool shiftKey = false)
    {
        string target = targetId is null ? "null" : $$"""{"id":"{{targetId}}","tagName":"INPUT"}""";
        return $$"""
            {"kind":"KeyboardEvent","type":"keydown","key":"{{key}}","code":"{{key}}","altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":{{(shiftKey ? "true" : "false")}},"pointerType":null,"detail":0,"buttons":0,
             "target":{{target}},"defaultPrevented":false,"timeStamp":7}
            """;
    }

    /// <summary>The snapshot JSON of a pointer event on the control.</summary>
    public static string PointerJson(string type = "pointerdown") =>
        $$"""
        {"kind":"PointerEvent","type":"{{type}}","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":0,"buttons":1,
         "target":null,"defaultPrevented":false,"timeStamp":9}
        """;

    /// <summary>The snapshot JSON of a native input change.</summary>
    public static string InputJson() =>
        """
        {"kind":"Event","type":"input","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":11}
        """;

    /// <summary>The snapshot JSON of a focus or a blur.</summary>
    public static string FocusJson(string type = "focus") =>
        $$"""
        {"kind":"FocusEvent","type":"{{type}}","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":13}
        """;

    /// <summary>The <see cref="SliderTwin"/> attached last.</summary>
    internal SliderTwin SliderTwin(int index = -1) =>
        ((DotNetObjectReference<SliderTwin>)SliderModule.Invocations["attach"][index < 0 ? ^1 : index].Arguments[2]!).Value;

    /// <summary>The registration-only <see cref="CompositeTwin"/> of the thumbs, attached last.</summary>
    internal CompositeTwin ThumbListTwin() =>
        ((DotNetObjectReference<CompositeTwin>)CompositeModule.Invocations["attach"][^1].Arguments[2]!).Value;

    /// <summary>Reports a value request, as the module does after its optimistic move.</summary>
    internal Task<int> RequestAsync<TComponent>(IRenderedComponent<TComponent> cut, string reason, int activeThumbIndex, double[] values, string? eventJson = null)
        where TComponent : IComponent
    {
        int next = ++revision;
        string json = $$"""{"reason":"{{reason}}","activeThumbIndex":{{activeThumbIndex}},"values":{{JsonSerializer.Serialize(values)}},"event":{{eventJson ?? (reason == "keyboard" ? KeyJson() : reason == "input-change" ? InputJson() : PointerJson())}},"revision":{{next}}}""";
        SliderTwin twin = SliderTwin();
        return cut.InvokeAsync(() => twin.OnValueRequest(json)).ContinueWith(_ => next, TaskScheduler.Default);
    }

    /// <summary>Reports a gesture change, as the module does.</summary>
    internal Task GestureAsync<TComponent>(IRenderedComponent<TComponent> cut, bool active, bool dragging, bool commit = false)
        where TComponent : IComponent
    {
        string json = $$"""{"active":{{Bool(active)}},"dragging":{{Bool(dragging)}},"commit":{{Bool(commit)}},"event":{{PointerJson(active ? "pointerdown" : "pointerup")}}}""";
        SliderTwin twin = SliderTwin();
        return cut.InvokeAsync(() => twin.OnGesture(json));
    }

    /// <summary>Reports a thumb input's focus or blur, as the module classifies it.</summary>
    internal Task FocusAsync<TComponent>(IRenderedComponent<TComponent> cut, int index, bool focused, bool outside = true)
        where TComponent : IComponent
    {
        string json = $$"""{"index":{{index}},"focused":{{Bool(focused)}},"outside":{{Bool(outside)}},"event":{{FocusJson(focused ? "focus" : "blur")}}}""";
        SliderTwin twin = SliderTwin();
        return cut.InvokeAsync(() => twin.OnFocusChange(json));
    }

    /// <summary>Reports the thumbs in document order, as the composite module does after the batch that changed them.</summary>
    internal Task ReportThumbsAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        IEnumerable<IElement> thumbs = cut.FindAll("[data-index]");
        CompositeTwin twin = ThumbListTwin();
        string json = JsonSerializer.Serialize(new { ids = thumbs.Select(element => element.Id).ToArray() });
        return cut.InvokeAsync(() => twin.OnItemsChange(json));
    }

    /// <summary>Requests a submit of the form, as the <c>form</c> module does: every range input is a valid control.</summary>
    internal async Task<FormSubmitAnswer> SubmitAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        JSRuntimeInvocation attach = FormModule.Invocations["attach"]
            .Last(i => JsonSerializer.Deserialize((string)i.Arguments[1]!, BlazixJsonContext.Default.FormOptions)!.Submit is not null);
        FormTwin twin = ((DotNetObjectReference<FormTwin>)attach.Arguments[2]!).Value;
        FormSubmitRequest request = new()
        {
            Event = new EventSnapshot { Kind = EventKind.Event, Type = "submit", DefaultPrevented = true, Target = new EventTargetInfo { TagName = "FORM" } },
            Controls = [.. cut.FindAll("input[type=range][id]").Select(input => new FormControlState
            {
                Id = input.GetAttribute("id")!,
                Value = input.GetAttribute("value") ?? "",
                Validity = NativeValidity.Default,
            })],
        };
        string json = JsonSerializer.Serialize(request, BlazixJsonContext.Default.FormSubmitRequest);
        string answer = await cut.InvokeAsync(() => twin.OnSubmitRequest(json));
        return JsonSerializer.Deserialize(answer, BlazixJsonContext.Default.FormSubmitAnswer)!;
    }

    /// <summary>Completes the exit of the last attached transitions handle (a field error's), as the module does.</summary>
    internal Task CompleteExitAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        TransitionsTwin twin = ((DotNetObjectReference<TransitionsTwin>)TransitionsModule.Invocations["attach"][^1].Arguments[2]!).Value;
        return cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
    }

    /// <summary>The rendered thumb inputs, in document order.</summary>
    public static IReadOnlyList<IElement> Inputs<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
        => cut.FindAll("input[type=range]");

    /// <summary>Builds the fragment of one part with the given parameters.</summary>
    public static RenderFragment Part<TPart>(Action<FragmentParameters<TPart>>? parameters = null)
        where TPart : IComponent
    {
        FragmentParameters<TPart> collected = new();
        parameters?.Invoke(collected);
        return builder =>
        {
            builder.OpenComponent<TPart>(0);
            foreach (KeyValuePair<string, object?> parameter in collected.Items)
            {
                builder.AddComponentParameter(1, parameter.Key, parameter.Value);
            }

            builder.CloseComponent();
        };
    }

    /// <summary>Builds the fragments one after another.</summary>
    public static RenderFragment Fragments(params RenderFragment[] fragments) => builder =>
    {
        foreach (RenderFragment fragment in fragments)
        {
            builder.AddContent(0, fragment);
        }
    };

    /// <summary>The anatomy of Base UI's TestSlider: a Value, then a Control holding a Track with an Indicator and the thumbs.</summary>
    public static RenderFragment Anatomy(params RenderFragment[] thumbs) =>
        Fragments(
            Part<SliderValue>(p => p.AddUnmatched("data-testid", "value")),
            Part<SliderControl>(p => p.AddUnmatched("data-testid", "control").AddChildContent(
                Part<SliderTrack>(t => t.AddUnmatched("data-testid", "track").AddChildContent(Fragments([
                    Part<SliderIndicator>(i => i.AddUnmatched("data-testid", "indicator")),
                    .. thumbs.Length == 0 ? [Thumb()] : thumbs,
                ]))))));

    /// <summary>Builds a thumb, with <c>data-testid</c> set to <c>thumb</c>, or <c>thumb-&lt;index&gt;</c> for an indexed one.</summary>
    public static RenderFragment Thumb(int? index = null, Action<FragmentParameters<SliderThumb>>? parameters = null) =>
        Part<SliderThumb>(p =>
        {
            if (index is { } i)
            {
                p.Add(x => x.Index, i);
            }

            p.AddUnmatched("data-testid", index is null ? "thumb" : $"thumb-{index}");
            parameters?.Invoke(p);
        });

    private static string Bool(bool value) => value ? "true" : "false";

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}

/// <summary>A host that renders its child content, for a slider inside a field, a form or an edit context.</summary>
public sealed class SliderHost : ComponentBase
{
    /// <summary>Gets or sets the content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);
}
