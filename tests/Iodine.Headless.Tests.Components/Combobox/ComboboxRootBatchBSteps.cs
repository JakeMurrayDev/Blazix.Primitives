using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

// The steps batch B's ComboboxRoot.test.tsx titles take on a ComboboxRootBatchBHarness (and, for the press, the Escape and
// the options sent, on any Combobox tree), as the modules report them on Server (the twins' [JSInvokable] callbacks), and
// the queries Base UI's titles make by role and name (getByRole('option', { name })).
/// <summary>Steps and queries over a rendered <see cref="ComboboxRootBatchBHarness"/> or any Combobox tree.</summary>
internal static partial class ComboboxRootBatchBSteps
{
    /// <summary>Gets the input's text, which the module writes: the store's input value, not the rendered attribute.</summary>
    /// <param name="cut">The rendered harness.</param>
    /// <returns>The store's input value.</returns>
    public static string InputText(this IRenderedComponent<ComboboxRootBatchBHarness> cut) =>
        cut.Instance.Root!.Store.State.InputValue;

    /// <summary>Gets a value indicating whether a listbox or grid is rendered.</summary>
    /// <param name="cut">The rendered component.</param>
    /// <returns><see langword="true"/> when one is rendered.</returns>
    public static bool HasList(this IRenderedComponent<ComboboxRootBatchBHarness> cut) =>
        cut.FindAll("[role=listbox],[role=grid]").Count != 0;

    /// <summary>Gets the one rendered option whose text is <paramref name="name"/>.</summary>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="name">The option's text.</param>
    /// <returns>The option.</returns>
    public static IElement Option(this IRenderedComponent<ComboboxRootBatchBHarness> cut, string name) =>
        Assert.Single(cut.FindAll("[role=option],[role=gridcell]"), option => option.TextContent == name);

    /// <summary>Gets a value indicating whether an option whose text is <paramref name="name"/> is rendered.</summary>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="name">The option's text.</param>
    /// <returns><see langword="true"/> when one is rendered.</returns>
    public static bool HasOption(this IRenderedComponent<ComboboxRootBatchBHarness> cut, string name) =>
        cut.FindAll("[role=option],[role=gridcell]").Any(option => option.TextContent == name);

    /// <summary>Clicks the option whose text is <paramref name="name"/>, with the event a browser's mouse click dispatches.</summary>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="name">The option's text.</param>
    /// <returns>A task that completes once the click was dispatched.</returns>
    public static Task ClickOptionAsync(this IRenderedComponent<ComboboxRootBatchBHarness> cut, string name) =>
        cut.InvokeAsync(() => cut.Option(name).Click(new MouseEventArgs { Type = "click", Detail = 1 }));

    /// <summary>Types into the input as the module reports a typed change on Server.</summary>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="test">The test context.</param>
    /// <param name="text">The input's text after the change.</param>
    /// <returns>A task that completes once the change was handled.</returns>
    public static Task TypeAsync(this IRenderedComponent<ComboboxRootBatchBHarness> cut, ComboboxTestContext test, string text) =>
        TypeIntoAsync(cut, test, text);

    /// <summary>Moves the highlight to an option as the list navigation reports an arrow key or a hover.</summary>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="test">The test context.</param>
    /// <param name="index">The option's index in the list.</param>
    /// <param name="name">The option's text.</param>
    /// <param name="eventJson">The snapshot of the event that moved the highlight; ArrowDown when not given.</param>
    /// <returns>A task that completes once the highlight was handled.</returns>
    public static Task HighlightAsync(this IRenderedComponent<ComboboxRootBatchBHarness> cut, ComboboxTestContext test, int index, string name, string? eventJson = null)
    {
        string json = $$"""{"index":{{index}},"itemId":"{{cut.Option(name).Id}}","event":{{eventJson ?? ComboboxTestContext.Key("ArrowDown")}},"revision":1000}""";
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string options && options.Contains("\"virtual\":true", StringComparison.Ordinal));
        CompositeTwin twin = ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
        return cut.InvokeAsync(() => twin.OnListHighlight(json));
    }

    /// <summary>Presses Enter on the input with an option highlighted, as the module reports it: the keydown, the option's click, the end.</summary>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="test">The test context.</param>
    /// <param name="name">The highlighted option's text.</param>
    /// <returns>A task that completes once the selection was handled.</returns>
    public static async Task EnterAsync(this IRenderedComponent<ComboboxRootBatchBHarness> cut, ComboboxTestContext test, string name)
    {
        ComboboxTwin input = test.TwinOf(ComboboxTwin.InputPart);
        await cut.InvokeAsync(() => input.OnSelectionEvent($$"""{"event":{{ComboboxTestContext.Key("Enter")}}}"""));
        await cut.ClickOptionAsync(name);
        await cut.InvokeAsync(() => input.OnSelectionEvent("""{"event":null}"""));
    }

    /// <summary>
    /// Ends the popup's exit as the <c>transitions</c> module reports it at once when no animation runs (JSDOM's case in
    /// Base UI): the popup layer routes the report to the store, which unmounts the popup.
    /// </summary>
    /// <param name="cut">The rendered harness.</param>
    /// <returns>A task that completes once the unmount has run.</returns>
    public static Task FinishExitAsync(this IRenderedComponent<ComboboxRootBatchBHarness> cut) =>
        cut.InvokeAsync(() => cut.Instance.Root!.Store.CompleteTransitionAsync(false));

    /// <summary>Ends the popup's exit of a <see cref="ComboboxTypedHarness{TValue}"/>, as the other overload does.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="cut">The rendered harness.</param>
    /// <returns>A task that completes once the unmount has run.</returns>
    public static Task FinishExitAsync<TValue>(this IRenderedComponent<ComboboxTypedHarness<TValue>> cut) =>
        cut.InvokeAsync(() => cut.Instance.Root!.Store.CompleteTransitionAsync(false));

    /// <summary>Presses the Trigger as the <c>interactions</c> module reports the press: an open, or a close when open.</summary>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="test">The test context.</param>
    /// <returns>A task that completes once the request was handled.</returns>
    public static Task PressTriggerAsync(this IRenderedComponent<ComboboxRootBatchBHarness> cut, ComboboxTestContext test) =>
        PressAsync(cut, test, cut.Find("[data-testid=trigger]").Id!, !cut.Instance.Root!.Store.IsOpen, "trigger-press");

    /// <summary>Types into the input of any Combobox tree as the module reports a typed change on Server.</summary>
    /// <typeparam name="TComponent">The type of the rendered component.</typeparam>
    /// <param name="cut">The rendered component.</param>
    /// <param name="test">The test context.</param>
    /// <param name="text">The input's text after the change.</param>
    /// <returns>A task that completes once the change was handled.</returns>
    public static Task TypeIntoAsync<TComponent>(IRenderedComponent<TComponent> cut, ComboboxTestContext test, string text)
        where TComponent : IComponent
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }

    /// <summary>Presses the input (its <c>data-testid</c> is <c>input</c>) as the <c>interactions</c> module reports the mousedown that opens the popup.</summary>
    /// <typeparam name="TComponent">The type of the rendered component.</typeparam>
    /// <param name="cut">The rendered component.</param>
    /// <param name="test">The test context.</param>
    /// <returns>A task that completes once the request was handled.</returns>
    public static Task PressInputAsync<TComponent>(this IRenderedComponent<TComponent> cut, ComboboxTestContext test)
        where TComponent : IComponent =>
        PressAsync(cut, test, cut.Find("[data-testid=input]").Id!, true, "input-press");

    /// <summary>Presses Escape as the <c>interactions</c> session reports the dismissal that closes the popup.</summary>
    /// <typeparam name="TComponent">The type of the rendered component.</typeparam>
    /// <param name="cut">The rendered component.</param>
    /// <param name="test">The test context.</param>
    /// <returns>A task that completes once the request was handled.</returns>
    public static Task EscapeAsync<TComponent>(this IRenderedComponent<TComponent> cut, ComboboxTestContext test)
        where TComponent : IComponent
    {
        InteractionsTwin twin = test.SessionTwin;
        string json = $$"""{"open":false,"reason":"escape-key","event":{{ComboboxTestContext.Key("Escape")}},"triggerId":null}""";
        return cut.InvokeAsync(() => twin.OnRequest(json));
    }

    /// <summary>Gets the options the <c>interactions</c> session handle sent last (the handle with a dismissal).</summary>
    /// <param name="test">The test context.</param>
    /// <returns>The JSON.</returns>
    public static string LastSessionOptions(this ComboboxTestContext test)
    {
        string options = (string)test.Interactions.Invocations["attach"].Last(attach => attach.Arguments[0] is null).Arguments[1]!;
        foreach (JSRuntimeInvocation update in test.InteractionsHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains("\"dismiss\":", StringComparison.Ordinal))
            {
                options = json;
            }
        }

        return options;
    }

    /// <summary>Gets the options an <c>interactions</c> trigger handle sent at its last attach.</summary>
    /// <param name="test">The test context.</param>
    /// <param name="triggerId">The trigger's id.</param>
    /// <returns>The JSON.</returns>
    public static string TriggerOptions(this ComboboxTestContext test, string triggerId) =>
        (string)test.Interactions.Invocations["attach"].Last(attach => attach.Arguments[1] is string json && json.Contains("\"triggerId\":\"" + triggerId + "\"", StringComparison.Ordinal)).Arguments[1]!;

    /// <summary>Gets every options JSON the <c>composite</c> handles sent, attaches then updates.</summary>
    /// <param name="test">The test context.</param>
    /// <returns>The JSON strings.</returns>
    public static List<string> CompositeOptions(this ComboboxTestContext test)
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

    /// <summary>Gets the options the list navigation's handles sent last (the options with <c>"virtual":true</c>).</summary>
    /// <param name="test">The test context.</param>
    /// <returns>The JSON, or the empty string.</returns>
    public static string LastListNavigationOptions(this ComboboxTestContext test) =>
        test.CompositeOptions().FindLast(options => options.Contains("\"virtual\":true", StringComparison.Ordinal)) ?? string.Empty;

    /// <summary>Gets every highlight request C# sent to the list navigation, in order, each once.</summary>
    /// <param name="test">The test context.</param>
    /// <returns>The <c>"highlight":{…}</c> fragments.</returns>
    public static List<string> HighlightRequests(this ComboboxTestContext test)
    {
        List<string> requests = [];
        foreach (string options in test.CompositeOptions())
        {
            foreach (Match match in HighlightPattern().Matches(options))
            {
                if (!requests.Contains(match.Value))
                {
                    requests.Add(match.Value);
                }
            }
        }

        return requests;
    }

    private static Task PressAsync<TComponent>(IRenderedComponent<TComponent> cut, ComboboxTestContext test, string id, bool open, string reason)
        where TComponent : IComponent
    {
        string marker = "\"triggerId\":\"" + id + "\"";
        JSRuntimeInvocation attach = test.Interactions.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains(marker, StringComparison.Ordinal));
        InteractionsTwin twin = ((DotNetObjectReference<InteractionsTwin>)attach.Arguments[2]!).Value;
        string json = $$"""{"open":{{(open ? "true" : "false")}},"reason":"{{reason}}","event":{{ComboboxTestContext.Click}},"triggerId":"{{id}}"}""";
        return cut.InvokeAsync(() => twin.OnRequest(json));
    }

    [GeneratedRegex("\"highlight\":\\{[^}]*\\}")]
    private static partial Regex HighlightPattern();
}
