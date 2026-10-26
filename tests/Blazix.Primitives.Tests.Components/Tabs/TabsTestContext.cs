using System.Linq.Expressions;
using System.Text.Json;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the Tabs tests: the library's services and bUnit's JS interop mock standing in for the
/// <c>button</c> module on each tab, the <c>composite</c> module on the list, the <c>transitions</c> module on each panel
/// and the <c>tabs</c> module on the list and each indicator. A test drives the twins as the modules would (ADR 06 §1):
/// <see cref="SelectAsync"/> is the Server report of an activating click, <see cref="DecideSelectAsync"/> the WebAssembly
/// decision inside it; <see cref="ReportDirectionAsync"/> and <see cref="ReportMeasurementAsync"/> are the module's
/// mirrors; <see cref="ReportTransitionAsync"/> and <see cref="CompleteExitAsync"/> are a panel's machine.
/// </summary>
public sealed class TabsTestContext : IDisposable
{
    public const string ButtonModulePath = "./_content/Blazix.Primitives/js/button.js";
    public const string CompositeModulePath = "./_content/Blazix.Primitives/js/composite.js";
    public const string TransitionsModulePath = "./_content/Blazix.Primitives/js/transitions.js";
    public const string TabsModulePath = "./_content/Blazix.Primitives/js/tabs.js";

    public TabsTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        ButtonModule = Context.JSInterop.SetupModule(ButtonModulePath);
        ButtonHandle = SetupHandle(ButtonModule);
        CompositeModule = Context.JSInterop.SetupModule(CompositeModulePath);
        CompositeHandle = SetupHandle(CompositeModule);
        TransitionsModule = Context.JSInterop.SetupModule(TransitionsModulePath);
        TransitionsHandle = SetupHandle(TransitionsModule);
        TabsModule = Context.JSInterop.SetupModule(TabsModulePath);
        TabsHandle = SetupHandle(TabsModule);
    }

    public BunitContext Context { get; } = new();

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    public BunitJSModuleInterop CompositeModule { get; }

    public BunitJSModuleInterop CompositeHandle { get; }

    public BunitJSModuleInterop TransitionsModule { get; }

    public BunitJSModuleInterop TransitionsHandle { get; }

    public BunitJSModuleInterop TabsModule { get; }

    public BunitJSModuleInterop TabsHandle { get; }

    /// <summary>The options JSON of every <c>tabs</c> attach, in order.</summary>
    public IReadOnlyList<string> TabsAttaches => [.. TabsModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!)];

    /// <summary>The options JSON of every <c>tabs</c> update, in order.</summary>
    public IReadOnlyList<string> TabsUpdates => [.. TabsHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!)];

    /// <summary>The options JSON of every <c>transitions</c> attach, in order.</summary>
    public IReadOnlyList<string> TransitionsAttaches => [.. TransitionsModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!)];

    /// <summary>The options JSON of every <c>composite</c> attach, in order.</summary>
    public IReadOnlyList<string> CompositeAttaches => [.. CompositeModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!)];

    /// <summary>The options JSON of every <c>composite</c> update, in order.</summary>
    public IReadOnlyList<string> CompositeUpdates => [.. CompositeHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!)];

    /// <summary>The options JSON of the composite's last attach or update.</summary>
    public string LastCompositeOptions => CompositeUpdates.Count > 0 ? CompositeUpdates[^1] : CompositeAttaches[^1];

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    /// <summary>The snapshot JSON of a click on a tab, as the module takes it inside the dispatch.</summary>
    public static string ClickJson(string? targetId = null, bool shiftKey = false)
    {
        string target = targetId is null ? "null" : $"\"{targetId}\"";
        return $$"""
            {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":{{(shiftKey ? "true" : "false")}},"pointerType":"mouse","detail":1,"buttons":0,
             "target":{"id":{{target}},"tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":7}
            """;
    }

    /// <summary>The snapshot JSON of a focus a tab received, as the module takes it inside the dispatch.</summary>
    public static string FocusJson(string? targetId = null)
    {
        string target = targetId is null ? "null" : $"\"{targetId}\"";
        return $$"""
            {"kind":"FocusEvent","type":"focusin","key":null,"code":null,"altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
             "target":{"id":{{target}},"tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":9}
            """;
    }

    /// <summary>The rendered tabs, in document order.</summary>
    public static IReadOnlyList<IElement> Tabs<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
        => cut.FindAll("[role=tab]");

    /// <summary>The rendered panels, in document order.</summary>
    public static IReadOnlyList<IElement> Panels<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
        => cut.FindAll("[role=tabpanel]");

    /// <summary>The <see cref="TabsTwin"/> of the list attached at <paramref name="index"/> among the lists, the last by default.</summary>
    internal TabsTwin ListTwin(int index = -1) => TabsTwinOf(TabsTwin.ListPart, index);

    /// <summary>The <see cref="TabsTwin"/> attached to the given element, a list or an indicator.</summary>
    internal TabsTwin TabsTwinOf(IElement element)
    {
        string? reference = element.GetAttribute("blazor:elementreference");
        JSRuntimeInvocation attach = TabsModule.Invocations["attach"].Last(i => i.Arguments[0] is ElementReference attached && string.Equals(attached.Id, reference, StringComparison.Ordinal));
        return ((DotNetObjectReference<TabsTwin>)attach.Arguments[2]!).Value;
    }

    /// <summary>The <see cref="TabsTwin"/> of the indicator attached at <paramref name="index"/> among the indicators, the last by default.</summary>
    internal TabsTwin IndicatorTwin(int index = -1) => TabsTwinOf(TabsTwin.IndicatorPart, index);

    /// <summary>The <see cref="TransitionsTwin"/> attached at <paramref name="index"/>, the last by default.</summary>
    internal TransitionsTwin TransitionsTwin(int index = -1) =>
        ((DotNetObjectReference<TransitionsTwin>)TransitionsModule.Invocations["attach"][index < 0 ? ^1 : index].Arguments[2]!).Value;

    /// <summary>The <see cref="CompositeTwin"/> attached last.</summary>
    internal CompositeTwin CompositeTwin() =>
        ((DotNetObjectReference<CompositeTwin>)CompositeModule.Invocations["attach"][^1].Arguments[2]!).Value;

    /// <summary>The <see cref="ButtonTwin"/> attached at <paramref name="index"/>, the last by default; tabs attach in render order.</summary>
    internal ButtonTwin ButtonTwin(int index = -1) =>
        ((DotNetObjectReference<ButtonTwin>)ButtonModule.Invocations["attach"][index < 0 ? ^1 : index].Arguments[2]!).Value;

    /// <summary>Reports an activating click or focus on a tab as the module does on Server, a round trip after the dispatch.</summary>
    internal Task SelectAsync<TComponent>(IRenderedComponent<TComponent> cut, string tabId, string direction = "none", string? eventJson = null, IElement? list = null)
        where TComponent : IComponent
    {
        TabsTwin twin = list is null ? ListTwin() : TabsTwinOf(list);
        string json = $$"""{"tabId":"{{tabId}}","direction":"{{direction}}","event":{{eventJson ?? ClickJson(tabId)}}}""";
        return cut.InvokeAsync(() => twin.OnSelect(json));
    }

    /// <summary>Hands an activation over and asks the selection decision as the module does on WebAssembly, inside the dispatch.</summary>
    internal Task<string> DecideSelectAsync<TComponent>(IRenderedComponent<TComponent> cut, string tabId, string direction = "none", string? eventJson = null, int list = -1)
        where TComponent : IComponent
    {
        TabsTwin twin = ListTwin(list);
        return cut.InvokeAsync(() =>
        {
            twin.OnSelecting($$"""{"tabId":"{{tabId}}","direction":"{{direction}}"}""");
            return twin.Decide(Blazix.Primitives.Interop.TabsTwin.SelectPoint, eventJson ?? ClickJson(tabId));
        });
    }

    /// <summary>Reports a click or focus on a tab that activated nothing, as the module does while asked to.</summary>
    internal Task ReportTabEventAsync<TComponent>(IRenderedComponent<TComponent> cut, string tabId, string eventJson, int list = -1)
        where TComponent : IComponent
    {
        TabsTwin twin = ListTwin(list);
        return cut.InvokeAsync(() => twin.OnTabEvent($$"""{"tabId":"{{tabId}}","event":{{eventJson}}}"""));
    }

    /// <summary>Reports the direction the module wrote on the parts.</summary>
    internal Task ReportDirectionAsync<TComponent>(IRenderedComponent<TComponent> cut, string direction, int list = -1)
        where TComponent : IComponent
    {
        TabsTwin twin = ListTwin(list);
        return cut.InvokeAsync(() => twin.OnDirectionChange($$"""{"direction":"{{direction}}"}"""));
    }

    /// <summary>Reports an indicator's measurement, as the module does once per change.</summary>
    internal Task ReportMeasurementAsync<TComponent>(IRenderedComponent<TComponent> cut, TabsTabPosition? position, TabsTabSize? size, int indicator = -1)
        where TComponent : IComponent
    {
        TabsTwin twin = IndicatorTwin(indicator);
        string json = JsonSerializer.Serialize(new TabsIndicatorMeasurement(position, size), BlazixJsonContext.Default.TabsIndicatorMeasurement);
        return cut.InvokeAsync(() => twin.OnMeasure(json));
    }

    /// <summary>Reports a transition status change of the panel attached at <paramref name="panel"/>, the last by default.</summary>
    internal Task ReportTransitionAsync<TComponent>(IRenderedComponent<TComponent> cut, string? status, bool mounted, int panel = -1)
        where TComponent : IComponent
    {
        TransitionsTwin twin = TransitionsTwin(panel);
        return cut.InvokeAsync(() => twin.OnStatusChange(JsonSerializer.Serialize(new { status, mounted })));
    }

    /// <summary>Reports that the exit of the panel attached at <paramref name="panel"/> finished, the last by default.</summary>
    internal Task CompleteExitAsync<TComponent>(IRenderedComponent<TComponent> cut, int panel = -1)
        where TComponent : IComponent
    {
        TransitionsTwin twin = TransitionsTwin(panel);
        return cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
    }

    /// <summary>Reports that the exit of every panel a transitions twin attached to finished, as the module does.</summary>
    internal async Task CompleteExitsAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        foreach (JSRuntimeInvocation attach in TransitionsModule.Invocations["attach"].ToArray())
        {
            // A panel that is gone disposed its twin's reference with it.
            TransitionsTwin twin;
            try
            {
                twin = ((DotNetObjectReference<TransitionsTwin>)attach.Arguments[2]!).Value;
            }
            catch (ObjectDisposedException)
            {
                continue;
            }

            await cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
        }
    }

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

    /// <summary>Builds a list holding the given content.</summary>
    public static RenderFragment List(params RenderFragment[] content) => Part<TabsList>(p => p.AddChildContent(Fragments(content)));

    /// <summary>Builds a tab with the given value, each with <c>data-testid</c> set to <c>tab-&lt;value&gt;</c>.</summary>
    public static RenderFragment Tab<TValue>(TValue value, Action<FragmentParameters<TabsTab<TValue>>>? parameters = null) =>
        Part<TabsTab<TValue>>(p =>
        {
            p.Add(x => x.Value, value);
            p.AddUnmatched("data-testid", $"tab-{value}");
            parameters?.Invoke(p);
        });

    /// <summary>Builds a panel with the given value, each with <c>data-testid</c> set to <c>panel-&lt;value&gt;</c>.</summary>
    public static RenderFragment Panel<TValue>(TValue value, Action<FragmentParameters<TabsPanel<TValue>>>? parameters = null) =>
        Part<TabsPanel<TValue>>(p =>
        {
            p.Add(x => x.Value, value);
            p.AddUnmatched("data-testid", $"panel-{value}");
            parameters?.Invoke(p);
        });

    /// <summary>Builds a kept-mounted panel with the given value.</summary>
    public static RenderFragment KeptPanel<TValue>(TValue value) => Panel(value, p => p.Add(x => x.KeepMounted, true));

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }

    private TabsTwin TabsTwinOf(string part, int index)
    {
        JSRuntimeInvocation[] attaches =
        [
            .. TabsModule.Invocations["attach"].Where(i => JsonSerializer.Deserialize((string)i.Arguments[1]!, BlazixJsonContext.Default.TabsOptions)!.Part == part),
        ];
        return ((DotNetObjectReference<TabsTwin>)attaches[index < 0 ? ^1 : index].Arguments[2]!).Value;
    }
}

/// <summary>The parameters of one part in a fragment the Tabs tests build, named as bUnit's builder names them.</summary>
/// <typeparam name="TPart">The part.</typeparam>
public sealed class FragmentParameters<TPart>
    where TPart : IComponent
{
    internal List<KeyValuePair<string, object?>> Items { get; } = [];

    /// <summary>Adds a parameter by its property.</summary>
    public FragmentParameters<TPart> Add<TValue>(Expression<Func<TPart, TValue>> parameter, TValue value)
    {
        Expression body = parameter.Body is UnaryExpression unary ? unary.Operand : parameter.Body;
        Items.Add(new(((MemberExpression)body).Member.Name, value));
        return this;
    }

    /// <summary>Adds an attribute the part captures as unmatched.</summary>
    public FragmentParameters<TPart> AddUnmatched(string name, object? value)
    {
        Items.Add(new(name, value));
        return this;
    }

    /// <summary>Adds the child content.</summary>
    public FragmentParameters<TPart> AddChildContent(RenderFragment content)
    {
        Items.Add(new("ChildContent", content));
        return this;
    }

    /// <summary>Adds the child content as text.</summary>
    public FragmentParameters<TPart> AddChildContent(string text) => AddChildContent(builder => builder.AddContent(0, text));
}
