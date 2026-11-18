using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the NumberField tests: the library's services, a captured logger, and bUnit's JS interop mock
/// standing in for the <c>button</c> module on each stepper. A test drives the steppers' twins as the module would (ADR 06
/// §1): <see cref="ClickAsync"/> is the single-action click report (a keyboard click or a tap, which Base UI's
/// <c>fireEvent.click</c> is), <see cref="PressAsync"/> a mouse press's start and first tick, <see cref="TickAsync"/> a
/// repeated tick and <see cref="ReleaseAsync"/> the release.
/// </summary>
public sealed class NumberFieldTestContext : IDisposable
{
    public const string ButtonModulePath = "./_content/Blazix.Primitives/js/button.js";
    public const string FormModulePath = "./_content/Blazix.Primitives/js/form.js";

    public NumberFieldTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.Services.AddSingleton<ILogger<NumberFieldRoot>>(Logger);
        ButtonModule = Context.JSInterop.SetupModule(ButtonModulePath);
        ButtonHandle = ButtonModule.SetupModule("attach", _ => true);
        ButtonHandle.SetupVoid("update", _ => true).SetVoidResult();
        ButtonHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        FormModule = Context.JSInterop.SetupModule(FormModulePath);
        FormHandle = FormModule.SetupModule("attach", _ => true);
        FormHandle.SetupVoid("update", _ => true).SetVoidResult();
        FormHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    public BunitContext Context { get; } = new();

    public CapturingLogger<NumberFieldRoot> Logger { get; } = new();

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    public BunitJSModuleInterop FormModule { get; }

    public BunitJSModuleInterop FormHandle { get; }

    /// <summary>The options JSON of every <c>button</c> update, in order.</summary>
    public IReadOnlyList<string> ButtonUpdates => [.. ButtonHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!)];

    /// <summary>The control options of every <c>form</c> attach and update, in order.</summary>
    internal IReadOnlyList<FormControlOptions> FormOptions =>
        [
            .. FormModule.Invocations["attach"].Select(i => JsonSerializer.Deserialize((string)i.Arguments[1]!, BlazixJsonContext.Default.FormOptions)!.Control!),
            .. FormHandle.Invocations["update"].Select(i => JsonSerializer.Deserialize((string)i.Arguments[0]!, BlazixJsonContext.Default.FormOptions)!.Control!),
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

    private static string Bool(bool value) => value ? "true" : "false";
}
