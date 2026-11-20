using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the OtpField tests (issue 278, M6 track 7): the library's services, captured loggers for the root and
/// the slots, and bUnit's JS interop mock standing in for the <c>composite</c> module (the slot registry), the <c>form</c>
/// module (the hidden input and slot 0's label discovery) and the <c>otp-field</c> module.
/// </summary>
public sealed class OtpFieldTestContext : IDisposable
{
    public const string CompositeModulePath = "./_content/Blazix.Primitives/js/composite.js";
    public const string FormModulePath = "./_content/Blazix.Primitives/js/form.js";
    public const string OtpFieldModulePath = "./_content/Blazix.Primitives/js/otp-field.js";

    /// <summary>Initializes a new instance of the <see cref="OtpFieldTestContext"/> class.</summary>
    public OtpFieldTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.Services.AddSingleton<ILogger<OtpFieldRoot>>(RootLogger);
        Context.Services.AddSingleton<ILogger<OtpFieldInput>>(InputLogger);
        CompositeModule = Context.JSInterop.SetupModule(CompositeModulePath);
        CompositeHandle = CompositeModule.SetupModule("attach", _ => true);
        CompositeHandle.SetupVoid("update", _ => true).SetVoidResult();
        CompositeHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        FormModule = Context.JSInterop.SetupModule(FormModulePath);
        FormHandle = FormModule.SetupModule("attach", _ => true);
        FormHandle.SetupVoid("update", _ => true).SetVoidResult();
        FormHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        OtpFieldModule = Context.JSInterop.SetupModule(OtpFieldModulePath);
        OtpFieldHandle = OtpFieldModule.SetupModule("attach", _ => true);
        OtpFieldHandle.SetupVoid("update", _ => true).SetVoidResult();
        OtpFieldHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    public BunitContext Context { get; } = new();

    public CapturingLogger<OtpFieldRoot> RootLogger { get; } = new();

    public CapturingLogger<OtpFieldInput> InputLogger { get; } = new();

    public BunitJSModuleInterop CompositeModule { get; }

    public BunitJSModuleInterop CompositeHandle { get; }

    public BunitJSModuleInterop FormModule { get; }

    public BunitJSModuleInterop FormHandle { get; }

    public BunitJSModuleInterop OtpFieldModule { get; }

    public BunitJSModuleInterop OtpFieldHandle { get; }

    /// <summary>The control options of every <c>form</c> attach, in order.</summary>
    internal IReadOnlyList<FormControlOptions> FormAttaches =>
        [.. FormModule.Invocations["attach"].Select(i => JsonSerializer.Deserialize((string)i.Arguments[1]!, BlazixJsonContext.Default.FormOptions)!.Control!)];

    /// <summary>Renders a root of six slots (Base UI's <c>OTPField</c> helper) with the given root parameters.</summary>
    /// <param name="parameters">Sets the root's parameters; <c>Length</c> is 6 unless set.</param>
    /// <returns>The rendered root.</returns>
    public IRenderedComponent<OtpFieldRoot> RenderSix(Action<ComponentParameterCollectionBuilder<OtpFieldRoot>>? parameters = null) =>
        Context.Render<OtpFieldRoot>(p =>
        {
            p.Add(r => r.Length, 6);
            parameters?.Invoke(p);
            p.AddChildContent(Slots(6));
        });

    /// <summary>A fragment of <paramref name="count"/> slots.</summary>
    /// <param name="count">The number of slots.</param>
    /// <returns>The fragment.</returns>
    public static RenderFragment Slots(int count) => builder =>
    {
        for (int i = 0; i < count; i++)
        {
            builder.OpenComponent<OtpFieldInput>(0);
            builder.SetKey(i);
            builder.CloseComponent();
        }
    };

    /// <summary>Reports the composite registry's order of the slots, as the module's first publication does.</summary>
    internal Task PublishSlotsAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        CompositeTwin twin = ((DotNetObjectReference<CompositeTwin>)CompositeModule.Invocations["attach"][^1].Arguments[2]!).Value;
        string ids = string.Join(",", cut.FindAll("input:not([aria-hidden])").Select(input => $"\"{input.Id}\""));
        return cut.InvokeAsync(() => twin.OnItemsChange($$"""{"ids":[{{ids}}]}"""));
    }

    /// <inheritdoc />
    // The options the `otp-field` module received: the attach's, then every update's, in order.
    internal IReadOnlyList<OtpFieldOptions> OtpOptions =>
        [
            .. OtpFieldModule.Invocations["attach"].Select(i => JsonSerializer.Deserialize((string)i.Arguments[1]!, BlazixJsonContext.Default.OtpFieldOptions)!),
            .. OtpFieldHandle.Invocations["update"].Select(i => JsonSerializer.Deserialize((string)i.Arguments[0]!, BlazixJsonContext.Default.OtpFieldOptions)!),
        ];

    internal OtpFieldTwin OtpTwin() =>
        OtpFieldModule.Invocations["attach"].Select(i => i.Arguments[2]).OfType<DotNetObjectReference<OtpFieldTwin>>().Last().Value;

    public static string EventJson(string type, string? key = null, string? targetId = null, bool ctrlKey = false) =>
        JsonSerializer.Serialize(new
        {
            kind = type == "keydown" ? "KeyboardEvent" : "Event",
            type,
            key,
            code = (string?)null,
            altKey = false,
            ctrlKey,
            metaKey = false,
            shiftKey = false,
            pointerType = (string?)null,
            detail = 0,
            buttons = 0,
            target = new { id = targetId, tagName = "INPUT" },
            defaultPrevented = false,
            timeStamp = 7,
        });

    public static string EditJson(int index, string kind, string raw, int revision, bool advanced = false)
    {
        string type = kind switch { "paste" => "paste", "input" => "input", _ => "keydown" };
        string? key = kind switch { "backspace" or "clear" => "Backspace", "delete" => "Delete", _ => null };
        string @event = EventJson(type, key, null, kind == "clear");
        return $$"""{"index":{{index}},"kind":"{{kind}}","raw":{{JsonSerializer.Serialize(raw)}},"advanced":{{(advanced ? "true" : "false")}},"revision":{{revision}},"event":{{@event}}}""";
    }

    // Server: the module reports the edit after the dispatch.
    internal Task EditAsync<TComponent>(IRenderedComponent<TComponent> cut, int index, string kind, string raw, int revision, bool advanced = false)
        where TComponent : IComponent
    {
        OtpFieldTwin twin = OtpTwin();
        return cut.InvokeAsync(() => twin.OnEdit(EditJson(index, kind, raw, revision, advanced)));
    }

    // WebAssembly: the module hands the edit over, then asks `edit`, whose chain runs inside the dispatch.
    internal Task<string> DecideEditAsync<TComponent>(IRenderedComponent<TComponent> cut, int index, string kind, string raw, int revision)
        where TComponent : IComponent
    {
        OtpFieldTwin twin = OtpTwin();
        string json = EditJson(index, kind, raw, revision);
        string @event = JsonDocument.Parse(json).RootElement.GetProperty("event").GetRawText();
        return cut.InvokeAsync(() =>
        {
            twin.OnEditHandover(json);
            return twin.Decide(OtpFieldTwin.EditPoint, @event);
        });
    }

    internal Task FocusAsync<TComponent>(IRenderedComponent<TComponent> cut, int index)
        where TComponent : IComponent
    {
        OtpFieldTwin twin = OtpTwin();
        return cut.InvokeAsync(() => twin.OnFocus($$"""{"index":{{index}},"event":{{EventJson("focusin")}}}"""));
    }

    internal Task BlurAsync<TComponent>(IRenderedComponent<TComponent> cut, int index)
        where TComponent : IComponent
    {
        OtpFieldTwin twin = OtpTwin();
        return cut.InvokeAsync(() => twin.OnBlur($$"""{"index":{{index}},"outside":true,"event":{{EventJson("focusout")}}}"""));
    }

    public void Dispose() => Context.Dispose();
}
