using System.Globalization;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A bUnit context for the Popover tests: the library's services, every module loose except <c>interactions</c>, whose
/// attaches are recorded so a test presses a trigger, hovers it or dismisses the popover as the module reports it on
/// Server (the twin's <c>[JSInvokable]</c> callbacks), and helpers that stand in for the <c>transitions</c> module's
/// completion report.
/// </summary>
internal sealed class PopoverTestContext : IDisposable
{
    public const string InteractionsPath = "./_content/Blazix.Primitives/js/interactions.js";

    private const string ClickTemplate = """
        {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"{0}","detail":{1},"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":1}
        """;

    private const string KeyTemplate = """
        {"kind":"KeyboardEvent","type":"keydown","key":"{0}","code":"{0}","altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":1}
        """;

    public PopoverTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Interactions = Context.JSInterop.SetupModule(InteractionsPath);
        InteractionsHandle = Interactions.SetupModule("attach", _ => true);
        InteractionsHandle.SetupVoid("update", _ => true).SetVoidResult();
        InteractionsHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    public BunitContext Context { get; } = new();

    public BunitJSModuleInterop Interactions { get; }

    public BunitJSModuleInterop InteractionsHandle { get; }

    /// <summary>Gets the snapshot JSON of a click, as the module snapshots it.</summary>
    /// <param name="pointerType">The pointer type, such as <c>mouse</c>.</param>
    /// <param name="detail">The click count; 0 for a keyboard activation.</param>
    /// <returns>The JSON.</returns>
    public static string Click(string pointerType = "mouse", int detail = 1) =>
        ClickTemplate.Replace("{0}", pointerType, StringComparison.Ordinal).Replace("{1}", detail.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>Gets the snapshot JSON of a keydown.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The JSON.</returns>
    public static string Key(string key) => KeyTemplate.Replace("{0}", key, StringComparison.Ordinal);

    public void Dispose() => Context.Dispose();

    /// <summary>Renders <paramref name="content"/> in a host whose values a test changes and re-renders.</summary>
    public IRenderedComponent<PopupTestHost> Render(Action<RenderTreeBuilder, PopupTestHost> content, Dictionary<string, object?>? values = null) =>
        Context.Render<PopupTestHost>(p => p.Add(x => x.Content, content).Add(x => x.InitialValues, values));

    /// <summary>Gets the twin of the trigger handle whose options carry <paramref name="triggerId"/>.</summary>
    public InteractionsTwin TriggerTwin(string triggerId)
    {
        string marker = "\"triggerId\":\"" + triggerId + "\"";
        IReadOnlyList<JSRuntimeInvocation> attaches = Interactions.Invocations["attach"];
        JSRuntimeInvocation attach = attaches.Last(invocation => invocation.Arguments[1] is string options && options.Contains(marker, StringComparison.Ordinal));
        return TwinOf(attach);
    }

    /// <summary>Gets the twin of the session handle (the attach whose target is <see langword="null"/>).</summary>
    public InteractionsTwin SessionTwin => TwinOf(Interactions.Invocations["attach"].Last(attach => attach.Arguments[0] is null));

    /// <summary>Presses a trigger as the module reports a press on Server: the request with its snapshot and interaction type.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="triggerId">The trigger's id.</param>
    /// <param name="open">The requested state, the module's toggle.</param>
    /// <param name="pointerType">The interaction type.</param>
    /// <param name="detail">The click count; 0 for a keyboard activation.</param>
    /// <returns>A task that completes when the press ran.</returns>
    public Task PressAsync(IRenderedComponent<PopupTestHost> cut, string triggerId, bool open, string pointerType = "mouse", int detail = 1)
    {
        string json = $$"""{"open":{{(open ? "true" : "false")}},"reason":"trigger-press","triggerId":"{{triggerId}}","interactionType":"{{pointerType}}","event":{{Click(pointerType, detail)}}}""";
        InteractionsTwin twin = TriggerTwin(triggerId);
        return cut.InvokeAsync(() => twin.OnPress(json));
    }

    /// <summary>Hovers a trigger as the module reports a hover open or leave.</summary>
    public Task HoverAsync(IRenderedComponent<PopupTestHost> cut, string triggerId, bool open)
    {
        string json = $$"""{"open":{{(open ? "true" : "false")}},"reason":"trigger-hover","event":{{Click()}},"triggerId":"{{triggerId}}"}""";
        InteractionsTwin twin = TriggerTwin(triggerId);
        return cut.InvokeAsync(() => twin.OnRequest(json));
    }

    /// <summary>Requests a change through the session, as the module reports Escape, an outside press or a focus-out.</summary>
    public Task SessionRequestAsync(IRenderedComponent<PopupTestHost> cut, bool open, string reason, string? eventJson = null)
    {
        string json = $$"""{"open":{{(open ? "true" : "false")}},"reason":"{{reason}}","event":{{eventJson ?? Click()}},"triggerId":null}""";
        InteractionsTwin twin = SessionTwin;
        return cut.InvokeAsync(() => twin.OnRequest(json));
    }

    /// <summary>Stands in for the popup's <c>transitions</c> report that its exit (or enter) finished.</summary>
    public static Task CompleteTransitionAsync<TPayload>(IRenderedComponent<PopupTestHost> cut, bool opened)
    {
        PopoverRoot<TPayload> root = cut.FindComponent<PopoverRoot<TPayload>>().Instance;
        return cut.InvokeAsync(() => root.Store.CompleteTransitionAsync(opened));
    }

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

    private static InteractionsTwin TwinOf(JSRuntimeInvocation attach) => ((DotNetObjectReference<InteractionsTwin>)attach.Arguments[2]!).Value;
}
