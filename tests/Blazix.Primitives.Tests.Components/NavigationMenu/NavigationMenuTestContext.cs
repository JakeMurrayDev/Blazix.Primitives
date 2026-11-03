using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the NavigationMenu tests: the library's services, every module loose, the <c>interactions</c> and
/// <c>navigation-menu</c> attaches recorded, and helpers that play the modules' reports as they arrive on Server (ADR 06
/// §1): a press, a hover, a session request, a trigger's activation, a focus-out, the popup's exit.
/// </summary>
internal sealed class NavigationMenuTestContext : IDisposable
{
    public const string InteractionsPath = "./_content/Blazix.Primitives/js/interactions.js";

    public const string NavigationMenuPath = "./_content/Blazix.Primitives/js/navigation-menu.js";

    public const string MouseEnter = """
        {"kind":"MouseEvent","type":"mouseenter","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":1}
        """;

    public const string Click = """
        {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":1,"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":1}
        """;

    public NavigationMenuTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Interactions = Context.JSInterop.SetupModule(InteractionsPath);
        InteractionsHandle = Interactions.SetupModule("attach", _ => true);
        InteractionsHandle.SetupVoid("update", _ => true).SetVoidResult();
        InteractionsHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        NavigationMenu = Context.JSInterop.SetupModule(NavigationMenuPath);
        NavigationMenuHandle = NavigationMenu.SetupModule("attach", _ => true);
        NavigationMenuHandle.SetupVoid("update", _ => true).SetVoidResult();
        NavigationMenuHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    public BunitContext Context { get; } = new();

    public BunitJSModuleInterop Interactions { get; }

    public BunitJSModuleInterop InteractionsHandle { get; }

    public BunitJSModuleInterop NavigationMenu { get; }

    public BunitJSModuleInterop NavigationMenuHandle { get; }

    /// <summary>Gets the snapshot JSON of a keydown.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The JSON.</returns>
    public static string Key(string key) => Click.Replace("\"PointerEvent\"", "\"KeyboardEvent\"", StringComparison.Ordinal)
        .Replace("\"click\"", "\"keydown\"", StringComparison.Ordinal)
        .Replace("\"key\":null", "\"key\":\"" + key + "\"", StringComparison.Ordinal);

    public void Dispose() => Context.Dispose();

    /// <summary>Renders the harness with the given parameters.</summary>
    public IRenderedComponent<NavigationMenuHarness> Render(Action<ComponentParameterCollectionBuilder<NavigationMenuHarness>>? parameters = null) =>
        Context.Render(parameters ?? (_ => { }));

    /// <summary>Gets the id a part rendered, found by its test id.</summary>
    public static string IdOf(IRenderedComponent<NavigationMenuHarness> cut, string testId) => cut.Find($"[data-testid=\"{testId}\"]").Id!;

    /// <summary>Presses a trigger as the module reports a press on Server.</summary>
    public Task PressAsync(IRenderedComponent<NavigationMenuHarness> cut, string testId, bool open)
    {
        string triggerId = IdOf(cut, testId);
        string json = $$"""{"open":{{(open ? "true" : "false")}},"reason":"trigger-press","triggerId":"{{triggerId}}","interactionType":"mouse","event":{{Click}}}""";
        InteractionsTwin twin = TriggerTwin(triggerId);
        return cut.InvokeAsync(() => twin.OnPress(json));
    }

    /// <summary>Hovers a trigger as the module reports a hover open or leave.</summary>
    public Task HoverAsync(IRenderedComponent<NavigationMenuHarness> cut, string testId, bool open)
    {
        string triggerId = IdOf(cut, testId);
        string json = $$"""{"open":{{(open ? "true" : "false")}},"reason":"trigger-hover","event":{{MouseEnter}},"triggerId":"{{triggerId}}"}""";
        InteractionsTwin twin = TriggerTwin(triggerId);
        return cut.InvokeAsync(() => twin.OnRequest(json));
    }

    /// <summary>Requests a change through the session, as the module reports Escape, an outside press or a focus-out.</summary>
    public Task SessionRequestAsync(IRenderedComponent<NavigationMenuHarness> cut, bool open, string reason)
    {
        string json = $$"""{"open":{{(open ? "true" : "false")}},"reason":"{{reason}}","event":{{Click}},"triggerId":null}""";
        InteractionsTwin twin = SessionTwin;
        return cut.InvokeAsync(() => twin.OnRequest(json));
    }

    /// <summary>Reports a trigger's activation as the <c>navigation-menu</c> module does.</summary>
    public static Task ActivateAsync(IRenderedComponent<NavigationMenuHarness> cut, string testId, string? reason, string? direction = null, string eventJson = MouseEnter)
    {
        NavigationMenuTwin twin = TriggerOf(cut, testId).ModuleTwin;
        string json = $$"""{"reason":{{Literal(reason)}},"direction":{{Literal(direction)}},"event":{{eventJson}}}""";
        return cut.InvokeAsync(() => twin.OnActivate(json));
    }

    /// <summary>Reports a trigger's focus-out as the <c>navigation-menu</c> module does.</summary>
    public static Task FocusOutAsync(IRenderedComponent<NavigationMenuHarness> cut, string testId)
    {
        NavigationMenuTwin twin = TriggerOf(cut, testId).ModuleTwin;
        return cut.InvokeAsync(() => twin.OnFocusOut("""{"event":null}"""));
    }

    /// <summary>Stands in for the popup's <c>transitions</c> report that its exit finished.</summary>
    public static Task CompleteExitAsync(IRenderedComponent<NavigationMenuHarness> cut)
    {
        NavigationMenuRoot<string> root = cut.Instance.Root!;
        return cut.InvokeAsync(() => root.Store.CompleteTransitionAsync(false));
    }

    /// <summary>Gets the rendered Trigger component with the given test id.</summary>
    public static NavigationMenuTrigger TriggerOf(IRenderedComponent<NavigationMenuHarness> cut, string testId) =>
        cut.FindComponents<NavigationMenuTrigger>()
            .First(trigger => trigger.Instance.AdditionalAttributes is { } attributes && attributes.TryGetValue("data-testid", out object? id) && (string?)id == testId)
            .Instance;

    /// <summary>Gets the options the session handle sent last: its attach's, or its latest update's.</summary>
    public string LastSessionOptions()
    {
        string options = (string)Interactions.Invocations["attach"].Last(attach => attach.Arguments[0] is null).Arguments[1]!;
        foreach (JSRuntimeInvocation update in InteractionsHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains("\"session\":", StringComparison.Ordinal))
            {
                options = json;
            }
        }

        return options;
    }

    /// <summary>Gets the options a trigger's interactions handle sent last.</summary>
    public string LastTriggerOptions(string triggerId)
    {
        string marker = "\"triggerId\":\"" + triggerId + "\"";
        string? options = null;
        foreach (JSRuntimeInvocation attach in Interactions.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains(marker, StringComparison.Ordinal))
            {
                options = json;
            }
        }

        foreach (JSRuntimeInvocation update in InteractionsHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains(marker, StringComparison.Ordinal))
            {
                options = json;
            }
        }

        return options!;
    }

    /// <summary>Gets the options the Root's <c>navigation-menu</c> handle sent last.</summary>
    public string LastRootModuleOptions()
    {
        string? options = null;
        foreach (JSRuntimeInvocation attach in NavigationMenu.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains("\"part\":\"root\"", StringComparison.Ordinal))
            {
                options = json;
            }
        }

        foreach (JSRuntimeInvocation update in NavigationMenuHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains("\"part\":\"root\"", StringComparison.Ordinal))
            {
                options = json;
            }
        }

        return options!;
    }

    private static string Literal(string? value) => value is null ? "null" : "\"" + value + "\"";

    private InteractionsTwin TriggerTwin(string triggerId)
    {
        string marker = "\"triggerId\":\"" + triggerId + "\"";
        JSRuntimeInvocation attach = Interactions.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string options && options.Contains(marker, StringComparison.Ordinal));
        return ((DotNetObjectReference<InteractionsTwin>)attach.Arguments[2]!).Value;
    }

    private InteractionsTwin SessionTwin =>
        ((DotNetObjectReference<InteractionsTwin>)Interactions.Invocations["attach"].Last(attach => attach.Arguments[0] is null).Arguments[2]!).Value;
}
