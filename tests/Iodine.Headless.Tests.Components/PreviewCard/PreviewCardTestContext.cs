using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the PreviewCard tests: the library's services, bUnit's JS interop mock in loose mode for the
/// <c>portal</c>, <c>positioning</c> and <c>transitions</c> modules, and a set-up <c>interactions</c> module whose twins a
/// test drives as the module would (ADR 06 §1): <see cref="RequestAsync"/> is a report of the module's hover, focus or
/// dismissal (ADR 04 Amendments 2026-10-27).
/// </summary>
internal sealed class PreviewCardTestContext : IDisposable
{
    public const string InteractionsModulePath = "./_content/Blazix.Primitives/js/interactions.js";

    public const string MouseJson = """
        {"kind":"MouseEvent","type":"mousemove","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":3.5}
        """;

    public const string FocusJson = """
        {"kind":"FocusEvent","type":"focus","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":4.5}
        """;

    public PreviewCardTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Interactions = Context.JSInterop.SetupModule(InteractionsModulePath);
        InteractionsHandle = Interactions.SetupModule("attach", _ => true);
        InteractionsHandle.SetupVoid("update", _ => true).SetVoidResult();
        InteractionsHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    public BunitContext Context { get; } = new();

    public BunitJSModuleInterop Interactions { get; }

    public BunitJSModuleInterop InteractionsHandle { get; }

    /// <summary>Gets the <c>interactions</c> attaches, in order.</summary>
    public IReadOnlyList<JSRuntimeInvocation> InteractionsAttaches => Interactions.Invocations["attach"];

    /// <summary>Gets the options of every <c>interactions</c> update, in order.</summary>
    public IReadOnlyList<string> InteractionsUpdates => [.. InteractionsHandle.Invocations["update"].Select(invocation => (string)invocation.Arguments[0]!)];

    /// <summary>Gets the twin of a session handle: the attach whose options carry the token of <paramref name="sessionToken"/>.</summary>
    /// <param name="sessionToken">The session token; the first session when <see langword="null"/>.</param>
    /// <returns>The twin.</returns>
    public InteractionsTwin SessionTwin(string? sessionToken = null) =>
        TwinOf(InteractionsAttaches.Last(attach => Role(attach) == "session" && (sessionToken is null || Token(attach) == sessionToken)));

    /// <summary>Gets the twin of the last floating handle: the Positioner's.</summary>
    /// <returns>The twin.</returns>
    public InteractionsTwin FloatingTwin() => TwinOf(InteractionsAttaches.Last(attach => Role(attach) == "floating"));

    /// <summary>Gets the options of every session attach, in order.</summary>
    public IReadOnlyList<JsonElement> SessionAttaches => [.. InteractionsAttaches.Where(attach => Role(attach) == "session").Select(Options)];

    /// <summary>Gets the twin of the trigger whose options name <paramref name="triggerId"/>.</summary>
    /// <param name="triggerId">The trigger id.</param>
    /// <returns>The twin.</returns>
    public InteractionsTwin TriggerTwin(string triggerId) =>
        TwinOf(InteractionsAttaches.Last(attach => Role(attach) == "trigger" && Options(attach).GetProperty("trigger").GetProperty("triggerId").GetString() == triggerId));

    /// <summary>Gets the last options a trigger's handle was sent, by attach or update.</summary>
    /// <param name="triggerId">The trigger id.</param>
    /// <returns>The trigger role's options.</returns>
    public JsonElement TriggerOptions(string triggerId)
    {
        JsonElement last = default;
        foreach (string json in InteractionsAttaches.Select(attach => (string)attach.Arguments[1]!).Concat(InteractionsUpdates))
        {
            JsonElement root = JsonDocument.Parse(json).RootElement;
            if (root.TryGetProperty("trigger", out JsonElement trigger) && trigger.TryGetProperty("triggerId", out JsonElement id) && id.GetString() == triggerId)
            {
                last = trigger;
            }
        }

        return last;
    }

    /// <summary>Reports a request of a trigger's or a session's handle, as the module does when a timer fires or an event arrives.</summary>
    /// <param name="twin">The handle's twin.</param>
    /// <param name="open">The requested open state.</param>
    /// <param name="reason">The reason's literal.</param>
    /// <param name="triggerId">The trigger the request names, or <see langword="null"/>.</param>
    /// <param name="eventJson">The event snapshot, or <see langword="null"/> for none.</param>
    /// <returns>A task that completes when the request has been dispatched.</returns>
    public Task RequestAsync(InteractionsTwin twin, bool open, string reason, string? triggerId = null, string? eventJson = MouseJson)
    {
        string payload = $$"""{"open":{{(open ? "true" : "false")}},"reason":"{{reason}}","event":{{eventJson ?? "null"}},"triggerId":{{(triggerId is null ? "null" : "\"" + triggerId + "\"")}},"instant":false}""";
        return Context.Renderer.Dispatcher.InvokeAsync(() => twin.OnRequest(payload));
    }

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    private static JsonElement Options(JSRuntimeInvocation attach) => JsonDocument.Parse((string)attach.Arguments[1]!).RootElement;

    private static string? Role(JSRuntimeInvocation attach)
    {
        JsonElement options = Options(attach);
        foreach (string role in new[] { "session", "trigger", "floating" })
        {
            if (options.TryGetProperty(role, out JsonElement value) && value.ValueKind == JsonValueKind.Object)
            {
                return role;
            }
        }

        return null;
    }

    private static string? Token(JSRuntimeInvocation attach) => Options(attach).TryGetProperty("sessionToken", out JsonElement token) ? token.GetString() : null;

    private static InteractionsTwin TwinOf(JSRuntimeInvocation attach) => ((DotNetObjectReference<InteractionsTwin>)attach.Arguments[2]!).Value;
}
