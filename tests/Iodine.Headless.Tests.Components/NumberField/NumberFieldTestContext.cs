using System.Text.Json;
using Iodine.Headless.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Iodine.Headless.Tests.Components;

/// <summary>
/// The bUnit context of the NumberField tests: the library's services, a captured logger, and bUnit's JS interop mock
/// standing in for the <c>button</c> module on each stepper. A test drives the steppers' twins as the module would (ADR 06
/// §1): <see cref="ClickAsync"/> is the single-action click report (a keyboard click or a tap, which Base UI's
/// <c>fireEvent.click</c> is), <see cref="PressAsync"/> a mouse press's start and first tick, <see cref="TickAsync"/> a
/// repeated tick and <see cref="ReleaseAsync"/> the release.
/// </summary>
public sealed class NumberFieldTestContext : IDisposable
{
    public const string ButtonModulePath = "./_content/Iodine.Headless/js/button.js";
    public const string FormModulePath = "./_content/Iodine.Headless/js/form.js";
    public const string NumberFieldModulePath = "./_content/Iodine.Headless/js/number-field.js";

    public const string InteractionsModulePath = "./_content/Iodine.Headless/js/interactions.js";

    /// <summary>Initializes a new instance of the <see cref="NumberFieldTestContext"/> class.</summary>
    /// <param name="ios">Whether the stubbed <c>readPlatformFlags</c> reports iOS (NUM-STA-19).</param>
    public NumberFieldTestContext(bool ios = false)
    {
        Context.JSInterop.SetupModule(InteractionsModulePath)
            .Setup<string>("readPlatformFlags")
            .SetResult(ios ? """{"voiceOver":false,"mac":false,"ios":true}""" : """{"voiceOver":false,"mac":false,"ios":false}""");
        Context.Services.AddIodineHeadless();
        Context.Services.AddSingleton<ILogger<NumberFieldRoot>>(Logger);
        ButtonModule = Context.JSInterop.SetupModule(ButtonModulePath);
        ButtonHandle = ButtonModule.SetupModule("attach", _ => true);
        ButtonHandle.SetupVoid("update", _ => true).SetVoidResult();
        ButtonHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        FormModule = Context.JSInterop.SetupModule(FormModulePath);
        FormHandle = FormModule.SetupModule("attach", _ => true);
        FormHandle.SetupVoid("update", _ => true).SetVoidResult();
        FormHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        NumberFieldModule = Context.JSInterop.SetupModule(NumberFieldModulePath);
        NumberFieldHandle = NumberFieldModule.SetupModule("attach", _ => true);
        NumberFieldHandle.SetupVoid("update", _ => true).SetVoidResult();
        NumberFieldHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    public BunitContext Context { get; } = new();

    public CapturingLogger<NumberFieldRoot> Logger { get; } = new();

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    public BunitJSModuleInterop FormModule { get; }

    public BunitJSModuleInterop FormHandle { get; }

    public BunitJSModuleInterop NumberFieldModule { get; }

    public BunitJSModuleInterop NumberFieldHandle { get; }

    /// <summary>The options JSON of every <c>number-field</c> attach and update of the visible input, in order.</summary>
    public IReadOnlyList<string> NumberFieldOptions =>
        [
            .. NumberFieldModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!).Where(IsInputOptions),
            .. NumberFieldHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!).Where(IsInputOptions),
        ];

    /// <summary>The options JSON of every <c>number-field</c> attach and update of a scrub area or a cursor, in order.</summary>
    public IReadOnlyList<string> ScrubOptions =>
        [
            .. NumberFieldModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!).Where(o => !IsInputOptions(o)),
            .. NumberFieldHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!).Where(o => !IsInputOptions(o)),
        ];

    /// <summary>The options JSON of every <c>button</c> update, in order.</summary>
    public IReadOnlyList<string> ButtonUpdates => [.. ButtonHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!)];

    /// <summary>The control options of every <c>form</c> attach and update, in order.</summary>
    internal IReadOnlyList<FormControlOptions> FormOptions =>
        [
            .. FormModule.Invocations["attach"].Select(i => JsonSerializer.Deserialize((string)i.Arguments[1]!, IodineJsonContext.Default.FormOptions)!.Control!),
            .. FormHandle.Invocations["update"].Select(i => JsonSerializer.Deserialize((string)i.Arguments[0]!, IodineJsonContext.Default.FormOptions)!.Control!),
        ];

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    /// <summary>The snapshot JSON of a pointer or mouse event, as the module takes it.</summary>
    public static string EventJson(string type, string pointerType = "mouse", int detail = 1, bool shiftKey = false, bool altKey = false)
    {
        return $$"""
            {"kind":"PointerEvent","type":"{{type}}","key":null,"code":null,"altKey":{{Bool(altKey)}},"ctrlKey":false,
             "metaKey":false,"shiftKey":{{Bool(shiftKey)}},"pointerType":"{{pointerType}}","detail":{{detail}},"buttons":1,
             "target":{"id":null,"tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":7}
            """;
    }

    /// <summary>The <see cref="ButtonTwin"/> attached to <paramref name="stepper"/>'s element.</summary>
    internal ButtonTwin ButtonTwin(IComponent stepper)
    {
        ElementReference? element = stepper switch
        {
            NumberFieldIncrement increment => increment.Element,
            NumberFieldDecrement decrement => decrement.Element,
            _ => throw new ArgumentException("Not a stepper.", nameof(stepper)),
        };
        JSRuntimeInvocation attach = ButtonModule.Invocations["attach"].Last(i => Equals(i.Arguments[0], element));
        return ((DotNetObjectReference<ButtonTwin>)attach.Arguments[2]!).Value;
    }

    /// <summary>Reports a single-action click, as the module does for a keyboard click or a tap.</summary>
    internal Task ClickAsync<TComponent>(IRenderedComponent<TComponent> cut, IComponent stepper, bool shiftKey = false, bool altKey = false)
        where TComponent : IComponent
    {
        ButtonTwin twin = ButtonTwin(stepper);
        string json = $$"""{"event":{{EventJson("click", "", 0, shiftKey, altKey)}}}""";
        return cut.InvokeAsync(() => twin.OnHoldClick(json));
    }

    /// <summary>Reports a mouse press: the start of a sequence and its first tick.</summary>
    internal Task PressAsync<TComponent>(IRenderedComponent<TComponent> cut, IComponent stepper, string pointerType = "mouse", bool shiftKey = false, bool altKey = false)
        where TComponent : IComponent
    {
        ButtonTwin twin = ButtonTwin(stepper);
        string json = $$"""{"event":{{EventJson("pointerdown", pointerType, 1, shiftKey, altKey)}},"start":true}""";
        return cut.InvokeAsync(() => twin.OnHoldTick(json));
    }

    /// <summary>Reports a repeated tick of the running sequence.</summary>
    internal Task TickAsync<TComponent>(IRenderedComponent<TComponent> cut, IComponent stepper, string pointerType = "mouse")
        where TComponent : IComponent
    {
        ButtonTwin twin = ButtonTwin(stepper);
        string json = $$"""{"event":{{EventJson("pointerdown", pointerType)}}}""";
        return cut.InvokeAsync(() => twin.OnHoldTick(json));
    }

    /// <summary>Reports the release of a sequence.</summary>
    internal Task ReleaseAsync<TComponent>(IRenderedComponent<TComponent> cut, IComponent stepper)
        where TComponent : IComponent
    {
        ButtonTwin twin = ButtonTwin(stepper);
        string json = $$"""{"event":{{EventJson("pointerup")}}}""";
        return cut.InvokeAsync(() => twin.OnHoldStop(json));
    }

    // The module owns the visible input's `value` from attach (C# → JS): the text the user sees is the last text C# sent.
    /// <summary>Gets the text the visible input shows: the last text sent to the <c>number-field</c> module.</summary>
    public string Text => JsonDocument.Parse(NumberFieldOptions[^1]).RootElement.GetProperty("value").GetString()!;

    /// <summary>The <see cref="NumberFieldTwin"/> attached last.</summary>
    internal NumberFieldTwin NumberFieldTwin() =>
        NumberFieldModule.Invocations["attach"].Select(i => i.Arguments[2]).OfType<DotNetObjectReference<NumberFieldTwin>>().Last().Value;

    /// <summary>The <see cref="NumberFieldScrubTwin"/> of the scrub area attached <paramref name="index"/>-th (0 for the first).</summary>
    internal NumberFieldScrubTwin ScrubTwin(int index = 0) =>
        NumberFieldModule.Invocations["attach"]
            .Where(i => ((string)i.Arguments[1]!).Contains(ScrubPart, StringComparison.Ordinal))
            .Select(i => ((DotNetObjectReference<NumberFieldScrubTwin>)i.Arguments[2]!).Value)
            .ElementAt(index);

    /// <summary>Reports the start of a scrub, as the module does after a primary pointerdown.</summary>
    internal Task ScrubStartAsync<TComponent>(IRenderedComponent<TComponent> cut, bool cursor = true, int area = 0)
        where TComponent : IComponent
    {
        NumberFieldScrubTwin twin = ScrubTwin(area);
        string json = JsonSerializer.Serialize(new { cursor, @event = new { kind = "PointerEvent", type = "pointerdown", pointerType = "mouse" } });
        return cut.InvokeAsync(() => twin.OnScrubStart(json));
    }

    /// <summary>Reports the outcome of the pointer-lock request.</summary>
    internal Task ScrubLockAsync<TComponent>(IRenderedComponent<TComponent> cut, bool denied, int area = 0)
        where TComponent : IComponent
    {
        NumberFieldScrubTwin twin = ScrubTwin(area);
        string json = JsonSerializer.Serialize(new { denied });
        return cut.InvokeAsync(() => twin.OnScrubLock(json));
    }

    /// <summary>Reports a scrub step with the move's delta on the scrub axis and the input's next revision.</summary>
    internal Task ScrubStepAsync<TComponent>(IRenderedComponent<TComponent> cut, double delta, bool shiftKey = false, bool altKey = false, int area = 0)
        where TComponent : IComponent
    {
        NumberFieldScrubTwin twin = ScrubTwin(area);
        string json = JsonSerializer.Serialize(new { delta, revision = ++inputRevision, @event = new { kind = "PointerEvent", type = "pointermove", pointerType = "mouse", shiftKey, altKey } });
        return cut.InvokeAsync(() => twin.OnScrubStep(json));
    }

    /// <summary>Reports the release that ends a scrub.</summary>
    internal Task ScrubEndAsync<TComponent>(IRenderedComponent<TComponent> cut, int area = 0)
        where TComponent : IComponent
    {
        NumberFieldScrubTwin twin = ScrubTwin(area);
        string json = JsonSerializer.Serialize(new { @event = new { kind = "PointerEvent", type = "pointerup" } });
        return cut.InvokeAsync(() => twin.OnScrubEnd(json));
    }

    private const string ScrubPart = "\"part\":\"scrub\"";

    private static bool IsInputOptions(string json) => !json.Contains("\"part\":", StringComparison.Ordinal);

    /// <summary>Reports a focus of the visible input, as the module does.</summary>
    internal Task FocusAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        NumberFieldTwin twin = NumberFieldTwin();
        return cut.InvokeAsync(() => twin.OnInputFocus("""{"event":{"kind":"FocusEvent","type":"focus"}}"""));
    }

    /// <summary>Reports a blur of the visible input with the text it shows, as the module does.</summary>
    internal Task BlurAsync<TComponent>(IRenderedComponent<TComponent> cut, string text, int revision = 1)
        where TComponent : IComponent
    {
        NumberFieldTwin twin = NumberFieldTwin();
        string json = JsonSerializer.Serialize(new { text, revision, @event = new { kind = "FocusEvent", type = "blur" } });
        return cut.InvokeAsync(() => twin.OnInputBlur(json));
    }

    private int inputRevision;

    /// <summary>Reports an allowed edit of the visible input, as the module does, with the next revision.</summary>
    internal Task ChangeAsync<TComponent>(IRenderedComponent<TComponent> cut, string text)
        where TComponent : IComponent
    {
        NumberFieldTwin twin = NumberFieldTwin();
        string json = JsonSerializer.Serialize(new { text, revision = ++inputRevision, @event = new { kind = "InputEvent", type = "input" } });
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }

    /// <summary>Reports a step key the module prevented, with the text the input showed and the next revision.</summary>
    internal Task KeyAsync<TComponent>(IRenderedComponent<TComponent> cut, string key, bool altKey = false, bool shiftKey = false, string? text = null)
        where TComponent : IComponent
    {
        NumberFieldTwin twin = NumberFieldTwin();
        string json = JsonSerializer.Serialize(new { key, text = text ?? Text, revision = ++inputRevision, @event = new { kind = "KeyboardEvent", type = "keydown", key, altKey, shiftKey } });
        return cut.InvokeAsync(() => twin.OnKeyStep(json));
    }

    /// <summary>Reports a paste the module prevented, with the spliced text and the next revision.</summary>
    internal Task PasteAsync<TComponent>(IRenderedComponent<TComponent> cut, string splicedText)
        where TComponent : IComponent
    {
        NumberFieldTwin twin = NumberFieldTwin();
        string json = JsonSerializer.Serialize(new { text = splicedText, revision = ++inputRevision, @event = new { kind = "ClipboardEvent", type = "paste" } });
        return cut.InvokeAsync(() => twin.OnInputPaste(json));
    }

    /// <summary>Reports a wheel step of the focused input.</summary>
    internal Task WheelAsync<TComponent>(IRenderedComponent<TComponent> cut, int direction, bool shiftKey = false, bool altKey = false)
        where TComponent : IComponent
    {
        NumberFieldTwin twin = NumberFieldTwin();
        string json = JsonSerializer.Serialize(new { direction, @event = new { kind = "WheelEvent", type = "wheel", shiftKey, altKey } });
        return cut.InvokeAsync(() => twin.OnWheelStep(json));
    }

    /// <summary>Reports a browser autofill of the hidden input.</summary>
    internal Task AutofillAsync<TComponent>(IRenderedComponent<TComponent> cut, double? value)
        where TComponent : IComponent
    {
        NumberFieldTwin twin = NumberFieldTwin();
        string json = JsonSerializer.Serialize(new { value, @event = new { kind = "Event", type = "change" } });
        return cut.InvokeAsync(() => twin.OnAutofill(json));
    }

    private static string Bool(bool value) => value ? "true" : "false";
}
