using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

// The steps a ComboboxRoot.test.tsx title takes on any of the Combobox hosts, as the modules report them on Server (the twins'
// [JSInvokable] callbacks), and the queries its assertions make.
// - The highlight Base UI asserts through `data-highlighted` and the input's `aria-activedescendant` is the store's active
//   index: the module writes both (ComboboxItemState.Highlighted is a mirrored field, ADR 04 Amendments 2026-11-03 (2)).
// - The `composite` module applies each highlight C# asks for and reports it back with the request's revision and no event
//   (composite.ts applyHighlightRequest, reportHighlight); every step ends with that report, as the module's answer to the
//   batch the step ran (EchoAsync).
/// <summary>Steps and queries over a rendered Combobox host, for the ComboboxRoot titles of batch C.</summary>
internal static class ComboboxRootBatchCSteps
{
    private static readonly ConditionalWeakTable<ComboboxTestContext, StrongBox<int>> s_applied = new();

    /// <summary>Gets the snapshot JSON of an ArrowDown keydown.</summary>
    public static string ArrowDown => ComboboxTestContext.Key("ArrowDown");

    /// <summary>Gets the snapshot JSON of a mouse move, the event of a hover's highlight.</summary>
    public static string MouseMove => ComboboxTestContext.Event(ComboboxTestContext.Click, "mousemove", 3);

    /// <summary>Types each character of <paramref name="text"/> after <paramref name="before"/>, one input event each, as <c>user.type</c> does.</summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="test">The test context.</param>
    /// <param name="cut">The rendered host.</param>
    /// <param name="text">The characters typed.</param>
    /// <param name="before">The input's text before the first character.</param>
    /// <returns>A task that completes once every change was handled.</returns>
    public static async Task TypeEachAsync<T>(this ComboboxTestContext test, IRenderedComponent<T> cut, string text, string before = "")
        where T : IComponent
    {
        for (int i = 1; i <= text.Length; i++)
        {
            await test.InputAsync(cut, before + text[..i]);
        }
    }

    /// <summary>Reports one typed input event with the input's text after it; <c>user.clear</c> is one with the empty text.</summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="test">The test context.</param>
    /// <param name="cut">The rendered host.</param>
    /// <param name="text">The input's text after the change.</param>
    /// <returns>A task that completes once the change was handled.</returns>
    public static async Task InputAsync<T>(this ComboboxTestContext test, IRenderedComponent<T> cut, string text)
        where T : IComponent
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        await cut.InvokeAsync(() => twin.OnInputChange(json));
        await test.EchoAsync(cut);
    }

    /// <summary>Gets the text of every rendered option, in order.</summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="cut">The rendered host.</param>
    /// <returns>The texts.</returns>
    public static List<string> OptionTexts<T>(this IRenderedComponent<T> cut)
        where T : IComponent =>
        cut.FindAll("[role=option]").Select(option => option.TextContent).ToList();

    /// <summary>Gets the one rendered option whose text is <paramref name="name"/>.</summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="cut">The rendered host.</param>
    /// <param name="name">The option's text.</param>
    /// <returns>The option.</returns>
    public static IElement OptionNamed<T>(this IRenderedComponent<T> cut, string name)
        where T : IComponent =>
        Assert.Single(cut.FindAll("[role=option]"), option => option.TextContent == name);

    /// <summary>Gets the text of the option the store highlights, or <see langword="null"/> for none.</summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="cut">The rendered host.</param>
    /// <param name="store">The root's store.</param>
    /// <returns>The highlighted option's text.</returns>
    public static string? HighlightedText<T>(this IRenderedComponent<T> cut, ComboboxStore store)
        where T : IComponent
    {
        if (store.State.ActiveIndex is not int index)
        {
            return null;
        }

        IReadOnlyList<IElement> options = cut.FindAll("[role=option]");
        return index >= 0 && index < options.Count ? options[index].TextContent : "(index " + index + ")";
    }

    /// <summary>Reports the rendered options' order, as the module does once items mount: the registry without <c>Items</c>.</summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="test">The test context.</param>
    /// <param name="cut">The rendered host.</param>
    /// <returns>A task that completes once the order was handled.</returns>
    public static async Task ReportItemsAsync<T>(this ComboboxTestContext test, IRenderedComponent<T> cut)
        where T : IComponent
    {
        string ids = string.Join(",", cut.FindAll("[role=option]").Select(option => "\"" + option.Id + "\""));
        CompositeTwin twin = test.CompositeTwin("\"reportItems\":true");
        await cut.InvokeAsync(() => twin.OnItemsChange("{\"ids\":[" + ids + "]}"));
        await test.EchoAsync(cut);
    }

    /// <summary>Reports the module's highlight of the option at <paramref name="index"/>, after a key or a hover.</summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="test">The test context.</param>
    /// <param name="cut">The rendered host.</param>
    /// <param name="index">The option's index.</param>
    /// <param name="eventJson">The snapshot of the event, such as <see cref="ArrowDown"/> or <see cref="MouseMove"/>.</param>
    /// <returns>A task that completes once the highlight was handled.</returns>
    public static async Task HighlightAsync<T>(this ComboboxTestContext test, IRenderedComponent<T> cut, int index, string eventJson)
        where T : IComponent
    {
        string id = cut.FindAll("[role=option]")[index].Id!;
        string json = $$"""{"index":{{index}},"itemId":"{{id}}","event":{{eventJson}},"revision":{{test.LastRequest()?.Revision ?? 0}}}""";
        CompositeTwin twin = test.ListTwin();
        await cut.InvokeAsync(() => twin.OnListHighlight(json));
        await test.EchoAsync(cut);
    }

    /// <summary>Reports ArrowDown on the closed input, as the module does: a request to open with <c>list-navigation</c>.</summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="test">The test context.</param>
    /// <param name="cut">The rendered host.</param>
    /// <returns>A task that completes once the request was handled.</returns>
    public static async Task ListOpenAsync<T>(this ComboboxTestContext test, IRenderedComponent<T> cut)
        where T : IComponent
    {
        string json = $$"""{"open":true,"reason":"list-navigation","event":{{ArrowDown}},"triggerId":null}""";
        CompositeTwin twin = test.CompositeTwin("\"reference\":{}");
        await cut.InvokeAsync(() => twin.OnListOpenRequest(json));
        await test.EchoAsync(cut);
    }

    /// <summary>Presses an element that opens the popup through its <c>interactions</c> trigger handle (the Trigger or the Input).</summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="test">The test context.</param>
    /// <param name="cut">The rendered host.</param>
    /// <param name="selector">The element's selector.</param>
    /// <param name="reason">The reason the module reports: <c>trigger-press</c> or <c>input-press</c>.</param>
    /// <param name="open">The state requested: <see langword="false"/> for a trigger's toggle of an open popup.</param>
    /// <returns>A task that completes once the request was handled.</returns>
    public static async Task PressAsync<T>(this ComboboxTestContext test, IRenderedComponent<T> cut, string selector, string reason, bool open = true)
        where T : IComponent
    {
        string id = cut.Find(selector).Id!;
        string marker = "\"triggerId\":\"" + id + "\"";
        JSRuntimeInvocation attach = test.Interactions.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains(marker, StringComparison.Ordinal));
        InteractionsTwin twin = ((DotNetObjectReference<InteractionsTwin>)attach.Arguments[2]!).Value;
        string state = open ? "true" : "false";
        await cut.InvokeAsync(() => twin.OnRequest($$"""{"open":{{state}},"reason":"{{reason}}","event":{{ComboboxTestContext.Click}},"triggerId":"{{id}}"}"""));
        await test.EchoAsync(cut);
    }

    /// <summary>
    /// Reports a native submission of the enclosing <see cref="Form"/>, as the <c>form</c> module does: the state it read from
    /// every enrolled control.
    /// </summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="test">The test context.</param>
    /// <param name="cut">The rendered host.</param>
    /// <param name="controls">The controls' states.</param>
    /// <returns>The form's answer.</returns>
    public static async Task<FormSubmitAnswer> SubmitAsync<T>(this ComboboxTestContext test, IRenderedComponent<T> cut, params FormControlState[] controls)
        where T : IComponent
    {
        JSRuntimeInvocation attach = test.Form.Invocations["attach"]
            .Last(invocation => invocation.Arguments[1] is string json && System.Text.Json.JsonSerializer.Deserialize(json, BlazixJsonContext.Default.FormOptions)!.Submit is not null);
        FormTwin twin = ((DotNetObjectReference<FormTwin>)attach.Arguments[2]!).Value;
        FormSubmitRequest request = new()
        {
            Event = new EventSnapshot { Kind = EventKind.Event, Type = "submit", DefaultPrevented = true, Target = new EventTargetInfo { TagName = "FORM" } },
            Controls = controls,
        };
        string payload = System.Text.Json.JsonSerializer.Serialize(request, BlazixJsonContext.Default.FormSubmitRequest);
        string answer = await cut.InvokeAsync(() => twin.OnSubmitRequest(payload));
        return System.Text.Json.JsonSerializer.Deserialize(answer, BlazixJsonContext.Default.FormSubmitAnswer)!;
    }

    /// <summary>Requests a close through the popup's session, as the module reports an outside press or Escape.</summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="test">The test context.</param>
    /// <param name="cut">The rendered host.</param>
    /// <param name="reason">The reason: <c>outside-press</c> or <c>escape-key</c>.</param>
    /// <returns>A task that completes once the request was handled.</returns>
    public static async Task DismissAsync<T>(this ComboboxTestContext test, IRenderedComponent<T> cut, string reason)
        where T : IComponent
    {
        InteractionsTwin twin = test.SessionTwin;
        string eventJson = reason == "escape-key" ? ComboboxTestContext.Key("Escape") : ComboboxTestContext.Click;
        await cut.InvokeAsync(() => twin.OnRequest($$"""{"open":false,"reason":"{{reason}}","event":{{eventJson}},"triggerId":null}"""));
        await test.EchoAsync(cut);
    }

    /// <summary>
    /// Presses Enter on the input as the module does (combobox.ts handleKey): it clicks the highlighted option, and without one
    /// asks an open, non-inline popup to close.
    /// </summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="test">The test context.</param>
    /// <param name="cut">The rendered host.</param>
    /// <param name="store">The root's store.</param>
    /// <returns>A task that completes once the key was handled.</returns>
    public static async Task EnterAsync<T>(this ComboboxTestContext test, IRenderedComponent<T> cut, ComboboxStore store)
        where T : IComponent
    {
        ComboboxTwin input = test.TwinOf(ComboboxTwin.InputPart);
        string enter = ComboboxTestContext.Key("Enter");
        IReadOnlyList<IElement> options = cut.FindAll("[role=option]");
        if (store.State.ActiveIndex is int index && index >= 0 && index < options.Count)
        {
            string id = options[index].Id!;
            await cut.InvokeAsync(() => input.OnSelectionEvent($$"""{"event":{{enter}}}"""));
            await cut.InvokeAsync(() => cut.Find("#" + id).Click());
            await cut.InvokeAsync(() => input.OnSelectionEvent("""{"event":null}"""));
        }
        else if (store.IsOpen && !store.State.Inline)
        {
            await cut.InvokeAsync(() => input.OnEnterClose($$"""{"event":{{enter}}}"""));
        }

        await test.EchoAsync(cut);
    }

    /// <summary>Clicks an option, as a pointer press does.</summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="test">The test context.</param>
    /// <param name="cut">The rendered host.</param>
    /// <param name="name">The option's text.</param>
    /// <returns>A task that completes once the click was handled.</returns>
    public static async Task ClickOptionNamedAsync<T>(this ComboboxTestContext test, IRenderedComponent<T> cut, string name)
        where T : IComponent
    {
        await cut.InvokeAsync(() => cut.OptionNamed(name).Click());
        await test.EchoAsync(cut);
    }

    /// <summary>Ends the popup's exit at once, as Base UI's tests run it with animations disabled.</summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="test">The test context.</param>
    /// <param name="cut">The rendered host.</param>
    /// <param name="store">The root's store.</param>
    /// <returns>A task that completes once the exit was handled.</returns>
    public static async Task CompleteCloseAsync<T>(this ComboboxTestContext test, IRenderedComponent<T> cut, ComboboxStore store)
        where T : IComponent
    {
        await cut.InvokeAsync(() => store.CompleteTransitionAsync(false));
        await test.EchoAsync(cut);
    }

    /// <summary>
    /// Answers the newest highlight C# asked for and the module has not applied, as the module does: it applies the request
    /// and reports the highlight with the request's revision and no event. A closed, non-inline list has no handle to answer.
    /// </summary>
    /// <typeparam name="T">The host's type.</typeparam>
    /// <param name="test">The test context.</param>
    /// <param name="cut">The rendered host.</param>
    /// <returns>A task that completes once the report was handled.</returns>
    public static async Task EchoAsync<T>(this ComboboxTestContext test, IRenderedComponent<T> cut)
        where T : IComponent
    {
        for (int round = 0; round < 4; round++)
        {
            if (test.LastRequest() is not { } request)
            {
                return;
            }

            StrongBox<int> applied = s_applied.GetOrCreateValue(test);
            if (request.Revision <= applied.Value || StoreOf(cut) is { } store && !store.IsOpen && !store.State.Inline)
            {
                return;
            }

            applied.Value = request.Revision;
            IReadOnlyList<IElement> options = cut.FindAll("[role=option]");
            string itemId = request.Index is int index && index < options.Count ? "\"" + options[index].Id + "\"" : "null";
            string index_ = request.Index is int at ? at.ToString(CultureInfo.InvariantCulture) : "null";
            string json = $$"""{"index":{{index_}},"itemId":{{itemId}},"event":null,"revision":{{request.Revision}}}""";
            CompositeTwin twin = test.ListTwin();
            await cut.InvokeAsync(() => twin.OnListHighlight(json));
        }
    }

    /// <summary>Gets the indexes of the highlights C# asked the list's handles for, in order (<see langword="null"/> for a clear).</summary>
    /// <param name="test">The test context.</param>
    /// <returns>The requested indexes.</returns>
    public static List<int?> HighlightRequests(this ComboboxTestContext test) => test.Requests().Select(request => request.Index).ToList();

    /// <summary>Gets the options the list navigation's handles sent last.</summary>
    /// <param name="test">The test context.</param>
    /// <returns>The JSON, or the empty string.</returns>
    public static string LastListOptions(this ComboboxTestContext test) =>
        test.ListOptions().FindLast(options => options.Contains("\"virtual\":true", StringComparison.Ordinal)) ?? string.Empty;

    private static (int? Index, int Revision)? LastRequest(this ComboboxTestContext test)
    {
        List<(int? Index, int Revision)> requests = test.Requests();
        return requests.Count == 0 ? null : requests[^1];
    }

    private static List<(int? Index, int Revision)> Requests(this ComboboxTestContext test)
    {
        List<(int? Index, int Revision)> requests = [];
        int revision = 0;
        foreach (string options in test.ListOptions())
        {
            Match match = Regex.Match(options, "\"highlight\":\\{(?:\"index\":(null|\\d+),)?\"revision\":(\\d+)\\}");
            int next = match.Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
            if (next > revision)
            {
                revision = next;
                requests.Add((match.Groups[1].Value is "" or "null" ? null : int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), next));
            }
        }

        return requests;
    }

    private static ComboboxStore? StoreOf<T>(IRenderedComponent<T> cut)
        where T : IComponent
    {
        object? root = cut.Instance.GetType().GetProperty("Root")?.GetValue(cut.Instance);
        return root?.GetType().GetProperty("Store", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(root) as ComboboxStore;
    }

    private static List<string> ListOptions(this ComboboxTestContext test)
    {
        List<string> options = [];
        foreach (JSRuntimeInvocation attach in test.Composite.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json)
            {
                options.Add(json);
            }
        }

        foreach (JSRuntimeInvocation update in test.CompositeHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json)
            {
                options.Add(json);
            }
        }

        return options;
    }

    // The list's floating handle, whose reports also move the items' mirrored highlight.
    private static CompositeTwin ListTwin(this ComboboxTestContext test) =>
        test.Composite.Invocations["attach"].Any(invocation => invocation.Arguments[1] is string json && json.Contains("\"floating\":", StringComparison.Ordinal))
            ? test.CompositeTwin("\"floating\":")
            : test.CompositeTwin("\"virtual\":true");

    private static CompositeTwin CompositeTwin(this ComboboxTestContext test, string marker)
    {
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains(marker, StringComparison.Ordinal));
        return ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
    }
}
