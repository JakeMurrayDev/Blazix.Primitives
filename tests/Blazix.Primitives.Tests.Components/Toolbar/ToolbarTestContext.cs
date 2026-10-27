using System.Text.Json;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the Toolbar tests: the library's services and bUnit's JS interop mock standing
/// in for the <c>composite</c> module on the root and the <c>button</c> module on each button and input. A test drives the
/// twins as the modules would (ADR 06 §1): <see cref="DecideNavigateAsync"/> is the root's navigation decision and
/// <see cref="ReportHandledKeyAsync"/> its Server report of a handled key.
/// </summary>
public sealed class ToolbarTestContext : IDisposable
{
    public const string ButtonModulePath = "./_content/Blazix.Primitives/js/button.js";
    public const string CompositeModulePath = "./_content/Blazix.Primitives/js/composite.js";

    public ToolbarTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        ButtonModule = Context.JSInterop.SetupModule(ButtonModulePath);
        ButtonHandle = SetupHandle(ButtonModule);
        CompositeModule = Context.JSInterop.SetupModule(CompositeModulePath);
        CompositeHandle = SetupHandle(CompositeModule);
    }

    public BunitContext Context { get; } = new();

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    public BunitJSModuleInterop CompositeModule { get; }

    public BunitJSModuleInterop CompositeHandle { get; }

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

    /// <summary>Gets the item ids an options JSON carries, in the order sent.</summary>
    public static string[] ItemIds(string optionsJson) =>
        [.. JsonDocument.Parse(optionsJson).RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()!)];

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

    /// <summary>The root element of a rendered toolbar.</summary>
    public static IElement Root<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
        => cut.Find("[role=toolbar]");

    /// <summary>The <see cref="ButtonTwin"/> attached at <paramref name="index"/>, the last by default.</summary>
    internal ButtonTwin ButtonTwin(int index = -1) =>
        ((DotNetObjectReference<ButtonTwin>)ButtonModule.Invocations["attach"][index < 0 ? ^1 : index].Arguments[2]!).Value;

    /// <summary>The <see cref="CompositeTwin"/> attached last.</summary>
    internal CompositeTwin CompositeTwin() =>
        ((DotNetObjectReference<CompositeTwin>)CompositeModule.Invocations["attach"][^1].Arguments[2]!).Value;

    /// <summary>Asks the root's navigation decision for a key on the item with the given element id, as the module does on WebAssembly.</summary>
    internal Task<string> DecideNavigateAsync<TComponent>(IRenderedComponent<TComponent> cut, string key, string itemId, string tagName = "BUTTON")
        where TComponent : IComponent
    {
        CompositeTwin twin = CompositeTwin();
        return cut.InvokeAsync(() => twin.Decide(Blazix.Primitives.Interop.CompositeTwin.NavigatePoint, KeyDownJson(key, itemId, tagName)));
    }

    /// <summary>Reports a key the root handled and stopped, as the <c>composite</c> module does on Server.</summary>
    internal Task ReportHandledKeyAsync<TComponent>(IRenderedComponent<TComponent> cut, string key, string itemId, string tagName = "BUTTON")
        where TComponent : IComponent
    {
        CompositeTwin twin = CompositeTwin();
        return cut.InvokeAsync(() => twin.OnKeyHandled($$"""{"event":{{KeyDownJson(key, itemId, tagName)}}}"""));
    }

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}
