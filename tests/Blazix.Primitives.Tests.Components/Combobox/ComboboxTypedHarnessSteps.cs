using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

// The steps a Combobox title takes on a ComboboxTypedHarness, as the modules report them on Server (the twins' [JSInvokable]
// callbacks), and the queries Base UI's titles make by role and name (getByRole('option', { name })).
/// <summary>Steps and queries over a rendered <see cref="ComboboxTypedHarness{TValue}"/>.</summary>
internal static class ComboboxTypedHarnessSteps
{
    /// <summary>Gets the input's text, which the module writes: the store's input value, not the rendered attribute.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="cut">The rendered harness.</param>
    /// <returns>The store's input value.</returns>
    public static string InputText<TValue>(this IRenderedComponent<ComboboxTypedHarness<TValue>> cut) =>
        cut.Instance.Root!.Store.State.InputValue;

    /// <summary>Gets the text of every rendered option, in order.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="cut">The rendered harness.</param>
    /// <returns>The texts.</returns>
    public static List<string> OptionTexts<TValue>(this IRenderedComponent<ComboboxTypedHarness<TValue>> cut) =>
        cut.FindAll("[role=option]").Select(option => option.TextContent).ToList();

    /// <summary>Gets the one rendered option whose text is <paramref name="name"/>.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="name">The option's text.</param>
    /// <returns>The option.</returns>
    public static IElement Option<TValue>(this IRenderedComponent<ComboboxTypedHarness<TValue>> cut, string name) =>
        Assert.Single(cut.FindAll("[role=option]"), option => option.TextContent == name);

    /// <summary>Gets a value indicating whether an option whose text is <paramref name="name"/> is rendered.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="name">The option's text.</param>
    /// <returns><see langword="true"/> when one is rendered.</returns>
    public static bool HasOption<TValue>(this IRenderedComponent<ComboboxTypedHarness<TValue>> cut, string name) =>
        cut.FindAll("[role=option]").Any(option => option.TextContent == name);

    /// <summary>Clicks the option whose text is <paramref name="name"/>.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="name">The option's text.</param>
    /// <returns>A task that completes once the click was dispatched.</returns>
    public static Task ClickOptionAsync<TValue>(this IRenderedComponent<ComboboxTypedHarness<TValue>> cut, string name) =>
        cut.InvokeAsync(() => cut.Option(name).Click());

    /// <summary>Types into the input as the module reports a typed change on Server.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="test">The test context.</param>
    /// <param name="text">The input's text after the change.</param>
    /// <returns>A task that completes once the change was handled.</returns>
    public static Task TypeAsync<TValue>(this IRenderedComponent<ComboboxTypedHarness<TValue>> cut, ComboboxTestContext test, string text)
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }

    /// <summary>Moves the highlight to an option as the list navigation reports an arrow key.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="test">The test context.</param>
    /// <param name="index">The option's index in the list.</param>
    /// <param name="name">The option's text.</param>
    /// <returns>A task that completes once the highlight was handled.</returns>
    public static Task HighlightAsync<TValue>(this IRenderedComponent<ComboboxTypedHarness<TValue>> cut, ComboboxTestContext test, int index, string name)
    {
        string json = $$"""{"index":{{index}},"itemId":"{{cut.Option(name).Id}}","event":{{ComboboxTestContext.Key("ArrowDown")}},"revision":1000}""";
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string options && options.Contains("\"virtual\":true", StringComparison.Ordinal));
        CompositeTwin twin = ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
        return cut.InvokeAsync(() => twin.OnListHighlight(json));
    }

    /// <summary>Presses Enter on the input with an option highlighted, as the module reports it: the keydown, the option's click, the end.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="test">The test context.</param>
    /// <param name="name">The highlighted option's text.</param>
    /// <returns>A task that completes once the selection was handled.</returns>
    public static async Task EnterAsync<TValue>(this IRenderedComponent<ComboboxTypedHarness<TValue>> cut, ComboboxTestContext test, string name)
    {
        ComboboxTwin input = test.TwinOf(ComboboxTwin.InputPart);
        await cut.InvokeAsync(() => input.OnSelectionEvent($$"""{"event":{{ComboboxTestContext.Key("Enter")}}}"""));
        await cut.ClickOptionAsync(name);
        await cut.InvokeAsync(() => input.OnSelectionEvent("""{"event":null}"""));
    }

    /// <summary>Autofills the hidden input as the <c>form</c> module reports a browser autofill.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="test">The test context.</param>
    /// <param name="text">The autofilled text.</param>
    /// <returns>A task that completes once the autofill was handled.</returns>
    public static Task AutofillAsync<TValue>(this IRenderedComponent<ComboboxTypedHarness<TValue>> cut, ComboboxTestContext test, string text)
    {
        JSRuntimeInvocation attach = test.Form.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains("\"reportAutofill\":true", StringComparison.Ordinal));
        FormTwin twin = ((DotNetObjectReference<FormTwin>)attach.Arguments[2]!).Value;
        string payload = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"defaultPrevented":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        return cut.InvokeAsync(() => twin.OnAutofill(payload));
    }

    /// <summary>Presses the Trigger as the <c>interactions</c> module reports the press that opens the popup.</summary>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    /// <param name="cut">The rendered harness.</param>
    /// <param name="test">The test context.</param>
    /// <returns>A task that completes once the open was handled.</returns>
    public static Task PressTriggerAsync<TValue>(this IRenderedComponent<ComboboxTypedHarness<TValue>> cut, ComboboxTestContext test)
    {
        string id = cut.Find("[data-testid=trigger]").Id!;
        string marker = "\"triggerId\":\"" + id + "\"";
        JSRuntimeInvocation attach = test.Interactions.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains(marker, StringComparison.Ordinal));
        InteractionsTwin twin = ((DotNetObjectReference<InteractionsTwin>)attach.Arguments[2]!).Value;
        return cut.InvokeAsync(() => twin.OnRequest($$"""{"open":true,"reason":"trigger-press","event":{{ComboboxTestContext.Click}},"triggerId":"{{id}}"}"""));
    }
}
