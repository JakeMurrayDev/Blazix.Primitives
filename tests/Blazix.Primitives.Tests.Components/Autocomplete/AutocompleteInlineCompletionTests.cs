using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196): the inline completion computed in C# from the highlight report (decision 11 (a), chosen by the
// user 2026-11-03; AUT-STA-05, STA-06 (d), API-15, API-17; AutocompleteRoot.tsx:90-105). The list handle's highlight is
// reported as the composite module reports it. L2 asserts the displayed text C# computes and sends (the store's text and
// the Input's update); the DOM is L4's.
/// <summary>Autocomplete's inline completion at L2.</summary>
public sealed class AutocompleteInlineCompletionTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = AutocompleteTesting.CreateContext();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "mode=\"both\": inline overlay + autocomplete handles filtering")]
    [Trait("Requirement", "AUT-STA-04")]
    [Trait("Requirement", "AUT-STA-05")]
    public async Task Both_shows_the_highlighted_text_and_keeps_filtering_by_the_typed_text()
    {
        IRenderedComponent<AutocompleteHarness> cut = Render(AutocompleteMode.Both);
        await TypeAsync(cut, "a");

        await HighlightAsync(cut, 1, "banana", ComboboxTestContext.Key("ArrowDown"));

        Assert.Equal("banana", Text(cut));
        Assert.Contains("\"value\":\"banana\"", test.LastInputOptions(), StringComparison.Ordinal);
        Assert.Equal(["apple", "banana"], cut.FindAll("[role=option]").Select(item => item.TextContent));
        Assert.Equal([("a", EventReason.InputChange)], cut.Instance.ValueChanges);
    }

    [Fact(DisplayName = "mode=\"both\": hovering items should not change the inline overlay (preserve temporary value)")]
    [Trait("Requirement", "AUT-STA-05")]
    public async Task Both_keeps_the_overlay_on_a_pointer_highlight()
    {
        IRenderedComponent<AutocompleteHarness> cut = Render(AutocompleteMode.Both);
        await TypeAsync(cut, "a");
        await HighlightAsync(cut, 0, "apple", ComboboxTestContext.Key("ArrowDown"));

        await HighlightAsync(cut, 1, "banana", ComboboxTestContext.Event(ComboboxTestContext.Click, "pointermove", 5));

        Assert.Equal("apple", Text(cut));
    }

    [Fact(DisplayName = "mode=\"both\": a non-pointer highlight clear drops the overlay and shows the typed text again")]
    [Trait("Requirement", "AUT-STA-06")]
    public async Task Both_drops_the_overlay_on_a_keyboard_clear()
    {
        IRenderedComponent<AutocompleteHarness> cut = Render(AutocompleteMode.Both);
        await TypeAsync(cut, "a");
        await HighlightAsync(cut, 0, "apple", ComboboxTestContext.Key("ArrowDown"));
        Assert.Equal("apple", Text(cut));

        await HighlightAsync(cut, -1, null, ComboboxTestContext.Key("ArrowUp"));

        Assert.Equal("a", Text(cut));
    }

    [Fact(DisplayName = "mode=\"inline\": static items with inline overlay, and mode=\"list\" and mode=\"none\" keep the typed text")]
    [Trait("Requirement", "AUT-STA-05")]
    public async Task Inline_shows_the_overlay_and_list_and_none_do_not()
    {
        foreach ((AutocompleteMode mode, string expected) in new[] { (AutocompleteMode.Inline, "banana"), (AutocompleteMode.List, "a"), (AutocompleteMode.None, "a") })
        {
            using ComboboxTestContext context = AutocompleteTesting.CreateContext();
            IRenderedComponent<AutocompleteHarness> cut = context.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, Parameters(mode)));
            await TypeAsync(context, cut, "a");

            await HighlightAsync(context, cut, 1, "banana", ComboboxTestContext.Key("ArrowDown"));

            Assert.True(expected == cut.Instance.Root!.Store.State.InputValue, $"{mode}: {cut.Instance.Root!.Store.State.InputValue}");
        }
    }

    [Fact(DisplayName = "does not fill the input with the highlighted item when readOnly")]
    [Trait("Requirement", "AUT-API-17")]
    public async Task Read_only_never_shows_the_overlay()
    {
        Dictionary<string, object?> parameters = Parameters(AutocompleteMode.Both);
        parameters["ReadOnly"] = true;
        parameters["DefaultValue"] = "a";
        IRenderedComponent<AutocompleteHarness> cut = test.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, parameters));

        await HighlightAsync(cut, 1, "banana", ComboboxTestContext.Key("ArrowDown"));

        Assert.Equal("a", Text(cut));
    }

    [Fact(DisplayName = "port: the consumer's OnItemHighlighted runs before the text follows the highlight")]
    [Trait("Requirement", "AUT-API-15")]
    public async Task On_item_highlighted_runs_before_the_overlay()
    {
        List<(string? Value, string Text)> seen = [];
        IRenderedComponent<AutocompleteHarness>? cut = null;
        Dictionary<string, object?> parameters = Parameters(AutocompleteMode.Both);
        parameters["OnItemHighlighted"] = EventCallback.Factory.Create<AutocompleteItemHighlightedEventArgs<string>>(
            this, args => seen.Add((args.Value, cut!.Instance.Root!.Store.State.InputValue)));
        cut = test.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, parameters));
        await TypeAsync(cut, "a");

        await HighlightAsync(cut, 1, "banana", ComboboxTestContext.Key("ArrowDown"));

        Assert.Equal([("banana", "a")], seen);
        Assert.Equal("banana", Text(cut));
    }

    private static Dictionary<string, object?> Parameters(AutocompleteMode mode) =>
        new() { ["Items"] = s_items, ["DefaultOpen"] = true, ["Mode"] = mode };

    private static string Text(IRenderedComponent<AutocompleteHarness> cut) => cut.Instance.Root!.Store.State.InputValue;

    private IRenderedComponent<AutocompleteHarness> Render(AutocompleteMode mode) =>
        test.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, Parameters(mode)));

    private Task TypeAsync(IRenderedComponent<AutocompleteHarness> cut, string text) => TypeAsync(test, cut, text);

    private Task HighlightAsync(IRenderedComponent<AutocompleteHarness> cut, int index, string? name, string eventJson) =>
        HighlightAsync(test, cut, index, name, eventJson);

    private static Task TypeAsync(ComboboxTestContext context, IRenderedComponent<AutocompleteHarness> cut, string text)
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = context.TwinOf(ComboboxTwin.InputPart);
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }

    private static Task HighlightAsync(ComboboxTestContext context, IRenderedComponent<AutocompleteHarness> cut, int index, string? name, string eventJson)
    {
        string itemId = name is null ? "null" : "\"" + cut.Find("[data-testid=item-" + name + "]").Id + "\"";
        string indexJson = index < 0 ? "null" : index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string json = $$"""{"index":{{indexJson}},"itemId":{{itemId}},"event":{{eventJson}},"revision":1000}""";
        JSRuntimeInvocation attach = context.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string options && options.Contains("\"virtual\":true", StringComparison.Ordinal));
        CompositeTwin twin = ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
        return cut.InvokeAsync(() => twin.OnListHighlight(json));
    }
}
