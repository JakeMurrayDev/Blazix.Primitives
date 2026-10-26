using System.Text.Json;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the Toggle and ToggleGroup tests: the library's services, a captured logger for each part, and
/// bUnit's JS interop mock standing in for the <c>button</c> module on each toggle and the <c>composite</c> module on a
/// group or a test root. A click is Blazor's own dispatch (<c>Click()</c>); a test drives the twins as the modules would
/// (ADR 06 §1): <see cref="DecideActivateAsync"/> is the WebAssembly activation decision of a key and
/// <see cref="DecideNavigateAsync"/> the composite's navigation decision.
/// </summary>
public sealed class ToggleTestContext : IDisposable
{
    public const string ButtonModulePath = "./_content/Blazix.Primitives/js/button.js";
    public const string CompositeModulePath = "./_content/Blazix.Primitives/js/composite.js";

    public ToggleTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.Services.AddSingleton<ILogger<Toggle>>(ToggleLogger);
        Context.Services.AddSingleton<ILogger<ToggleGroup>>(GroupLogger);
        ButtonModule = Context.JSInterop.SetupModule(ButtonModulePath);
        ButtonHandle = SetupHandle(ButtonModule);
        CompositeModule = Context.JSInterop.SetupModule(CompositeModulePath);
        CompositeHandle = SetupHandle(CompositeModule);
    }

    public BunitContext Context { get; } = new();

    public CapturingLogger<Toggle> ToggleLogger { get; } = new();

    public CapturingLogger<ToggleGroup> GroupLogger { get; } = new();

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    public BunitJSModuleInterop CompositeModule { get; }

    public BunitJSModuleInterop CompositeHandle { get; }

    /// <summary>The messages written to the toggles' logger, in order.</summary>
    public IReadOnlyList<string> ToggleMessages => [.. ToggleLogger.Entries.Select(entry => entry.Message)];

    /// <summary>The messages written to the groups' logger, in order.</summary>
    public IReadOnlyList<string> GroupMessages => [.. GroupLogger.Entries.Select(entry => entry.Message)];

    /// <summary>The options JSON of every <c>button</c> attach, in order.</summary>
    public IReadOnlyList<string> ButtonAttaches => [.. ButtonModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!)];

    /// <summary>The options JSON of every <c>button</c> update, in order.</summary>
    public IReadOnlyList<string> ButtonUpdates => [.. ButtonHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!)];

    /// <summary>The options JSON of every <c>composite</c> attach, in order.</summary>
    public IReadOnlyList<string> CompositeAttaches => [.. CompositeModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!)];

    /// <summary>The options JSON of every <c>composite</c> update, in order.</summary>
    public IReadOnlyList<string> CompositeUpdates => [.. CompositeHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!)];

    /// <summary>The options JSON of the composite's last attach or update.</summary>
    public string LastCompositeOptions => CompositeUpdates.Count > 0 ? CompositeUpdates[^1] : CompositeAttaches[^1];

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    /// <summary>The snapshot JSON of a keydown, as the modules take it for their decisions.</summary>
    public static string KeyDownJson(string key, string? targetId, string tagName = "BUTTON")
    {
        string target = targetId is null ? "null" : $"\"{targetId}\"";
        return $$"""
            {"kind":"KeyboardEvent","type":"keydown","key":"{{key}}","code":"{{key}}","altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
             "target":{"id":{{target}},"tagName":"{{tagName}}"},"defaultPrevented":false,"timeStamp":5}
            """;
    }

    /// <summary>The rendered toggles (every element with <c>aria-pressed</c>), in document order.</summary>
    public static IReadOnlyList<IElement> Toggles<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
        => cut.FindAll("[aria-pressed]");

    /// <summary>Gets the item ids an options JSON carries, in the order sent.</summary>
    public static string[] ItemIds(string optionsJson) =>
        [.. JsonDocument.Parse(optionsJson).RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()!)];

    /// <summary>Builds toggles with the given values, each with <c>data-testid</c> set to its value; a <see langword="null"/> value is omitted.</summary>
    public static RenderFragment Toggles(params string?[] values) => builder =>
    {
        for (int i = 0; i < values.Length; i++)
        {
            builder.OpenComponent<Toggle>(0);
            builder.SetKey(i);
            if (values[i] is { } value)
            {
                builder.AddComponentParameter(1, nameof(Toggle.Value), value);
            }

            builder.AddAttribute(2, "data-testid", "toggle-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.CloseComponent();
        }
    };

    /// <summary>The <see cref="ButtonTwin"/> attached at <paramref name="index"/>, the last by default; toggles attach in render order.</summary>
    internal ButtonTwin ButtonTwin(int index = -1) =>
        ((DotNetObjectReference<ButtonTwin>)ButtonModule.Invocations["attach"][index < 0 ? ^1 : index].Arguments[2]!).Value;

    /// <summary>The <see cref="CompositeTwin"/> attached last.</summary>
    internal CompositeTwin CompositeTwin() =>
        ((DotNetObjectReference<CompositeTwin>)CompositeModule.Invocations["attach"][^1].Arguments[2]!).Value;

    /// <summary>Asks toggle <paramref name="toggle"/>'s activation decision for a keydown, as the <c>button</c> module does on WebAssembly.</summary>
    internal Task<string> DecideActivateAsync<TComponent>(IRenderedComponent<TComponent> cut, string key, int toggle = -1)
        where TComponent : IComponent
    {
        ButtonTwin target = ButtonTwin(toggle);
        return cut.InvokeAsync(() => target.Decide(Blazix.Primitives.Interop.ButtonTwin.ActivatePoint, KeyDownJson(key, null)));
    }

    /// <summary>Asks the root's navigation decision for a key on the item with the given element id, as the <c>composite</c> module does on WebAssembly.</summary>
    internal Task<string> DecideNavigateAsync<TComponent>(IRenderedComponent<TComponent> cut, string key, string itemId)
        where TComponent : IComponent
    {
        CompositeTwin twin = CompositeTwin();
        return cut.InvokeAsync(() => twin.Decide(Blazix.Primitives.Interop.CompositeTwin.NavigatePoint, KeyDownJson(key, itemId)));
    }

    /// <summary>Reports a key the root handled and stopped, as the <c>composite</c> module does on Server.</summary>
    internal Task ReportHandledKeyAsync<TComponent>(IRenderedComponent<TComponent> cut, string key, string itemId)
        where TComponent : IComponent
    {
        CompositeTwin twin = CompositeTwin();
        return cut.InvokeAsync(() => twin.OnKeyHandled($$"""{"event":{{KeyDownJson(key, itemId)}}}"""));
    }

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}
