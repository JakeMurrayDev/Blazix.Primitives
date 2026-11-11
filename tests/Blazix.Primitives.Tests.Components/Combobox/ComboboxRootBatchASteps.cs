using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

// The steps a ComboboxRoot.test.tsx title (batch A) takes on a ComboboxRootBatchAHost, as the modules report them on Server
// (the twins' [JSInvokable] callbacks), and the queries Base UI's titles make by role and name. user.click(input) is the
// `interactions` input-press report, user.type the `combobox` input reports one character at a time, user.clear one empty
// change, Escape the session's request, the end of an exit animation the popup's `transitions` report, and the items'
// registration order the list's `composite` report (Base UI's items register on commit; the module reports the DOM order).
/// <summary>Steps and queries over a rendered <see cref="ComboboxRootBatchAHost"/>.</summary>
internal static class ComboboxRootBatchASteps
{
    /// <summary>Gets the root's store.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <returns>The store.</returns>
    public static ComboboxStore<string> Store(this IRenderedComponent<ComboboxRootBatchAHost> cut) => cut.Instance.Root!.Store;

    /// <summary>Gets the input's text, which the module writes: the store's input value, not the rendered attribute.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <returns>The store's input value.</returns>
    public static string InputText(this IRenderedComponent<ComboboxRootBatchAHost> cut) => cut.Store().State.InputValue;

    /// <summary>Gets a value indicating whether a listbox is rendered, Base UI's <c>queryByRole('listbox')</c>.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <returns><see langword="true"/> when one is rendered.</returns>
    public static bool HasListbox(this IRenderedComponent<ComboboxRootBatchAHost> cut) => cut.FindAll("[role=listbox]").Count != 0;

    /// <summary>Gets the text of every rendered option, in order.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <returns>The texts.</returns>
    public static List<string> OptionTexts(this IRenderedComponent<ComboboxRootBatchAHost> cut) =>
        cut.FindAll("[role=option]").Select(option => option.TextContent).ToList();

    /// <summary>Gets the one rendered option whose text is <paramref name="name"/>.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="name">The option's text.</param>
    /// <returns>The option.</returns>
    public static IElement Option(this IRenderedComponent<ComboboxRootBatchAHost> cut, string name) =>
        Assert.Single(cut.FindAll("[role=option]"), option => option.TextContent == name);

    /// <summary>Clicks the option whose text is <paramref name="name"/>.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="name">The option's text.</param>
    /// <returns>A task that completes once the click was dispatched.</returns>
    public static Task ClickOptionAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut, string name) =>
        cut.InvokeAsync(() => cut.Option(name).Click());

    /// <summary>Presses the input as the <c>interactions</c> module reports the press that opens the popup (input-press).</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="test">The test context.</param>
    /// <returns>A task that completes once the open was handled.</returns>
    public static Task PressInputAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut, ComboboxTestContext test) =>
        RequestThroughTriggerAsync(cut, test, cut.Find("[data-testid=input]").Id!, "input-press");

    /// <summary>Presses the Trigger as the <c>interactions</c> module reports the press that opens the popup (trigger-press).</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="test">The test context.</param>
    /// <returns>A task that completes once the open was handled.</returns>
    public static Task PressTriggerAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut, ComboboxTestContext test) =>
        RequestThroughTriggerAsync(cut, test, cut.Find("[data-testid=trigger]").Id!, "trigger-press");

    /// <summary>Types into the input as the module reports one typed change.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="test">The test context.</param>
    /// <param name="text">The input's text after the change.</param>
    /// <param name="typed">Whether the change is typed input; <see langword="false"/> for an autofill-like change.</param>
    /// <returns>A task that completes once the change was handled.</returns>
    public static Task InputAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut, ComboboxTestContext test, string text, bool typed = true)
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":{{(typed ? "true" : "false")}},"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }

    /// <summary>Types <paramref name="text"/> after the input's text one character at a time, as user.type does.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="test">The test context.</param>
    /// <param name="text">The typed characters.</param>
    /// <returns>A task that completes once every change was handled.</returns>
    public static async Task TypeAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut, ComboboxTestContext test, string text)
    {
        foreach (char character in text)
        {
            await cut.InputAsync(test, cut.InputText() + character);
        }
    }

    /// <summary>Empties the input, as user.clear does.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="test">The test context.</param>
    /// <returns>A task that completes once the change was handled.</returns>
    public static Task ClearAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut, ComboboxTestContext test) => cut.InputAsync(test, string.Empty);

    /// <summary>Presses Escape, as the session reports the close request.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="test">The test context.</param>
    /// <returns>A task that completes once the request was handled.</returns>
    public static Task EscapeAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut, ComboboxTestContext test)
    {
        string json = $$"""{"open":false,"reason":"escape-key","event":{{ComboboxTestContext.Key("Escape")}},"triggerId":null}""";
        InteractionsTwin twin = test.SessionTwin;
        return cut.InvokeAsync(() => twin.OnRequest(json));
    }

    /// <summary>Presses outside the popup, as the session reports the close request.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="test">The test context.</param>
    /// <returns>A task that completes once the request was handled.</returns>
    public static Task OutsidePressAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut, ComboboxTestContext test)
    {
        string json = $$"""{"open":false,"reason":"outside-press","event":{{ComboboxTestContext.Event(ComboboxTestContext.Click, "mousedown", 3)}},"triggerId":null}""";
        InteractionsTwin twin = test.SessionTwin;
        return cut.InvokeAsync(() => twin.OnRequest(json));
    }

    /// <summary>Ends the popup's exit animation, as its <c>transitions</c> handle reports it (Base UI's tests disable animations).</summary>
    /// <param name="cut">The rendered host.</param>
    /// <returns>A task that completes once the completion was handled.</returns>
    public static Task CompleteCloseAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut) =>
        cut.InvokeAsync(() => cut.Store().CompleteTransitionAsync(false));

    /// <summary>Reports the rendered options' order, as the list's <c>composite</c> handle does after the items changed.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="test">The test context.</param>
    /// <returns>A task that completes once the report was handled.</returns>
    public static Task ReportOrderAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut, ComboboxTestContext test)
    {
        string ids = string.Join(",", cut.FindAll("[role=option]").Select(option => System.Text.Json.JsonSerializer.Serialize(option.Id)));
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains("\"reportItems\":true", StringComparison.Ordinal));
        CompositeTwin twin = ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
        return cut.InvokeAsync(() => twin.OnItemsChange("{\"ids\":[" + ids + "]}"));
    }

    /// <summary>Moves the highlight to an option as the list navigation reports an arrow key.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="test">The test context.</param>
    /// <param name="index">The option's index in the list.</param>
    /// <param name="name">The option's text.</param>
    /// <returns>A task that completes once the highlight was handled.</returns>
    public static Task HighlightAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut, ComboboxTestContext test, int index, string name)
    {
        string json = $$"""{"index":{{index}},"itemId":"{{cut.Option(name).Id}}","event":{{ComboboxTestContext.Key("ArrowDown")}},"revision":1000}""";
        return cut.InvokeAsync(() => ListNavigationTwin(test).OnListHighlight(json));
    }

    /// <summary>Clicks an option as user.click does: the pointer moves onto it first, which the list navigation reports as a hover highlight.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="test">The test context.</param>
    /// <param name="name">The option's text.</param>
    /// <returns>A task that completes once the click was dispatched.</returns>
    public static async Task PointerClickOptionAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut, ComboboxTestContext test, string name)
    {
        int index = cut.FindAll("[role=option]").ToList().FindIndex(option => option.TextContent == name);
        string hover = ComboboxTestContext.Event(ComboboxTestContext.Click, "mousemove", 2);
        string json = $$"""{"index":{{index}},"itemId":"{{cut.Option(name).Id}}","event":{{hover}},"revision":1000}""";
        await cut.InvokeAsync(() => ListNavigationTwin(test).OnListHighlight(json));
        await cut.ClickOptionAsync(name);
    }

    /// <summary>Gets the selected index the list navigation sends its handles (the item the module highlights on open), or <see langword="null"/>.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <returns>The index.</returns>
    public static int? ListSelectedIndex(this IRenderedComponent<ComboboxRootBatchAHost> cut) => cut.Store().ListNavigation.List.SelectedIndex;

    /// <summary>Presses Enter on the input with an option highlighted, as the module reports it: the keydown, the option's click, the end.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="test">The test context.</param>
    /// <param name="name">The highlighted option's text.</param>
    /// <returns>A task that completes once the selection was handled.</returns>
    public static async Task EnterAsync(this IRenderedComponent<ComboboxRootBatchAHost> cut, ComboboxTestContext test, string name)
    {
        ComboboxTwin input = test.TwinOf(ComboboxTwin.InputPart);
        await cut.InvokeAsync(() => input.OnSelectionEvent($$"""{"event":{{ComboboxTestContext.Key("Enter")}}}"""));
        await cut.InvokeAsync(() => cut.Option(name).Click());
        await cut.InvokeAsync(() => input.OnSelectionEvent("""{"event":null}"""));
    }

    /// <summary>Autofills the hidden input as the <c>form</c> module reports a browser autofill.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="test">The test context.</param>
    /// <param name="text">The autofilled text.</param>
    /// <returns>A task that completes once the autofill was handled.</returns>
    public static Task AutofillAsync<TComponent>(this IRenderedComponent<TComponent> cut, ComboboxTestContext test, string text)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        JSRuntimeInvocation attach = test.Form.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains("\"reportAutofill\":true", StringComparison.Ordinal));
        FormTwin twin = ((DotNetObjectReference<FormTwin>)attach.Arguments[2]!).Value;
        string payload = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"defaultPrevented":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        return cut.InvokeAsync(() => twin.OnAutofill(payload));
    }

    /// <summary>Gets every options JSON the list navigation's handles sent, attaches then updates.</summary>
    /// <param name="test">The test context.</param>
    /// <returns>The options, in order.</returns>
    public static List<string> ListHandleOptions(this ComboboxTestContext test)
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

    /// <summary>Gets the highlight index a request to the list navigation carried last, or <see langword="null"/> for none.</summary>
    /// <param name="test">The test context.</param>
    /// <param name="since">The number of options sent before the step under test.</param>
    /// <returns>The index; -1 for a request that clears the highlight.</returns>
    public static int? LastHighlightRequest(this ComboboxTestContext test, int since = 0)
    {
        System.Text.RegularExpressions.Regex request = new("\"highlight\":\\{([^}]*)\\}");
        System.Text.RegularExpressions.Regex index = new("\"index\":(-?\\d+)");
        int? last = null;
        foreach (string options in test.ListHandleOptions().Skip(since))
        {
            System.Text.RegularExpressions.Match match = request.Match(options);
            if (match.Success)
            {
                System.Text.RegularExpressions.Match at = index.Match(match.Groups[1].Value);
                last = at.Success ? int.Parse(at.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
            }
        }

        return last;
    }

    private static CompositeTwin ListNavigationTwin(ComboboxTestContext test)
    {
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string options && options.Contains("\"virtual\":true", StringComparison.Ordinal));
        return ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
    }

    private static Task RequestThroughTriggerAsync(IRenderedComponent<ComboboxRootBatchAHost> cut, ComboboxTestContext test, string id, string reason)
    {
        string marker = "\"triggerId\":\"" + id + "\"";
        JSRuntimeInvocation attach = test.Interactions.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains(marker, StringComparison.Ordinal));
        InteractionsTwin twin = ((DotNetObjectReference<InteractionsTwin>)attach.Arguments[2]!).Value;
        return cut.InvokeAsync(() => twin.OnRequest($$"""{"open":true,"reason":"{{reason}}","event":{{ComboboxTestContext.Click}},"triggerId":"{{id}}"}"""));
    }
}
