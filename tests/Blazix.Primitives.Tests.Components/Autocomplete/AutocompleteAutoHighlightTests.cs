using Blazix.Primitives.Interop;
using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196): AutocompleteRoot's AutoHighlight over track 12's AutoHighlightValue (design point 4; AUT-API-05,
// STA-11, STA-12, §16 Q4; AutocompleteRoot.test.tsx "prop: autoHighlight"): null and false are off, true highlights the
// first match after typing, Always keeps a highlight whenever the list has items. L2 asserts the highlight C# requests.
/// <summary>Autocomplete's AutoHighlight at L2.</summary>
public sealed class AutocompleteAutoHighlightTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = AutocompleteTesting.CreateContext();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "highlights the first item when typing and keeps it during filtering")]
    [Trait("Requirement", "AUT-API-05")]
    [Trait("Requirement", "AUT-STA-12")]
    public async Task Highlights_the_first_item_when_typing()
    {
        IRenderedComponent<AutocompleteHarness> cut = Render(new() { ["Items"] = s_items, ["DefaultOpen"] = true, ["AutoHighlight"] = (AutoHighlightValue?)true });
        Assert.Null(cut.Instance.Root!.Store.State.ActiveIndex);

        await TypeAsync(cut, "a");
        Assert.Equal(0, cut.Instance.Root!.Store.State.ActiveIndex);

        await TypeAsync(cut, "an");
        Assert.Equal(0, cut.Instance.Root!.Store.State.ActiveIndex);
        Assert.Equal(["banana"], cut.FindAll("[role=option]").Select(item => item.TextContent));
    }

    [Fact(DisplayName = "port: AutoHighlight false or omitted highlights nothing while typing")]
    [Trait("Requirement", "AUT-API-05")]
    public async Task False_or_omitted_highlights_nothing()
    {
        foreach (Dictionary<string, object?> parameters in new[]
        {
            new Dictionary<string, object?> { ["Items"] = s_items, ["DefaultOpen"] = true, ["AutoHighlight"] = (AutoHighlightValue?)false },
            new Dictionary<string, object?> { ["Items"] = s_items, ["DefaultOpen"] = true },
        })
        {
            using ComboboxTestContext context = AutocompleteTesting.CreateContext();
            IRenderedComponent<AutocompleteHarness> cut = context.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, parameters));
            await TypeAsync(context, cut, "a");

            Assert.Null(cut.Instance.Root!.Store.State.ActiveIndex);
            Assert.Equal(2, cut.FindAll("[role=option]").Count);
        }
    }

    [Fact(DisplayName = "highlights the first item immediately when behavior is \"always\"")]
    [Trait("Requirement", "AUT-API-05")]
    [Trait("Requirement", "AUT-STA-11")]
    public async Task Always_highlights_the_first_item_immediately()
    {
        IRenderedComponent<AutocompleteHarness> cut = Render(new() { ["Items"] = s_items, ["DefaultOpen"] = true, ["AutoHighlight"] = (AutoHighlightValue?)AutoHighlightValue.Always });

        await TypeAsync(cut, "c");

        Assert.Equal(0, cut.Instance.Root!.Store.State.ActiveIndex);
        Assert.Equal(["cherry"], cut.FindAll("[role=option]").Select(item => item.TextContent));
    }

    // §16 Q4 (AriaCombobox.tsx:287-292, AutocompleteRoot.tsx:90-105; decision 14, replicated): the automatic highlight is
    // reported with the reason none, which the completion takes, so the input shows the whole first match at once.
    [Fact(DisplayName = "port: AutoHighlight with the inline completion fills the first match as soon as the user types")]
    [Trait("Requirement", "AUT-STA-05")]
    [Trait("Requirement", "AUT-API-05")]
    public async Task Auto_highlight_with_the_completion_fills_the_first_match()
    {
        IRenderedComponent<AutocompleteHarness> cut = Render(new()
        {
            ["Items"] = s_items,
            ["DefaultOpen"] = true,
            ["Mode"] = AutocompleteMode.Both,
            ["AutoHighlight"] = (AutoHighlightValue?)true,
        });

        await TypeAsync(cut, "b");

        Assert.Equal("banana", cut.Instance.Root!.Store.State.InputValue);
        Assert.Equal([("b", EventReason.InputChange)], cut.Instance.ValueChanges);
    }

    // §16 Q3, AUT-STA-08 (ComboboxItem.tsx:110-135; design point 1 (ii), settled by the orchestrator 2026-11-12): without
    // `Items`, a mounted Item equal to the activated value registers its index as the selected index, which an opening arrow
    // with AutoHighlight highlights; no Item is ever selected.
    [Fact(DisplayName = "port: without items, a remounted item equal to the activated value registers its index as the selected index")]
    [Trait("Requirement", "AUT-STA-08")]
    [Trait("Requirement", "AUT-STA-03")]
    public async Task A_remounted_item_registers_the_activated_value_as_the_selected_index()
    {
        IRenderedComponent<AutocompleteStaticHost> cut = test.Context.Render<AutocompleteStaticHost>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true, ["AutoHighlight"] = (AutoHighlightValue?)true }));
        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());
        Assert.False(cut.Instance.Root!.Store.State.Lifecycle.EffectiveOpen);
        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());
        Assert.Empty(cut.FindAll("[role=option]"));

        string reopen = $$"""{"open":true,"reason":"trigger-press","event":{{ComboboxTestContext.Click}},"triggerId":null}""";
        Blazix.Primitives.Interop.InteractionsTwin session = test.SessionTwin;
        await cut.InvokeAsync(() => session.OnRequest(reopen));
        Assert.Equal(3, cut.FindAll("[role=option]").Count);

        // The remounted items' order, as the list's composite handle reports it once they registered.
        string ids = string.Join(",", cut.FindAll("[role=option]").Select(option => System.Text.Json.JsonSerializer.Serialize(option.Id)));
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains("\"reportItems\":true", StringComparison.Ordinal));
        CompositeTwin registry = ((Microsoft.JSInterop.DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
        await cut.InvokeAsync(() => registry.OnItemsChange("{\"ids\":[" + ids + "]}"));

        Assert.Equal(1, cut.Instance.Root!.Store.State.SelectedIndex);
        Assert.All(cut.FindAll("[role=option]"), item => Assert.False(item.HasAttribute("data-selected")));
    }

    // ComboboxRoot.test.tsx's Autocomplete title (AUT-STA-11): the List renders directly under the root, so it stays visible
    // when the cleared text closes the popup, and Always restores the first item (AriaCombobox.tsx:1124-1130).
    [Fact(DisplayName = "restores the first highlight after an always-highlighted query becomes empty")]
    [Trait("Requirement", "AUT-STA-11")]
    public async Task Restores_the_first_highlight_after_an_always_highlighted_query_becomes_empty()
    {
        IRenderedComponent<AutocompleteInlineHost> cut = test.Context.Render<AutocompleteInlineHost>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?>
        {
            ["Items"] = (IReadOnlyList<string>)["apple"], ["DefaultOpen"] = true, ["AutoHighlight"] = (AutoHighlightValue?)AutoHighlightValue.Always,
        }));
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        await cut.InvokeAsync(() => twin.OnInputChange($$"""{"text":"z","typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}"""));
        Assert.Empty(cut.FindAll("[role=option]"));

        await cut.InvokeAsync(() => twin.OnInputChange($$"""{"text":"","typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}"""));

        Assert.Single(cut.FindAll("[role=option]"));
        Assert.Equal(0, cut.Instance.Root!.Store.State.ActiveIndex);
    }

    private IRenderedComponent<AutocompleteHarness> Render(Dictionary<string, object?> parameters) =>
        test.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, parameters));

    private Task TypeAsync(IRenderedComponent<AutocompleteHarness> cut, string text) => TypeAsync(test, cut, text);

    private static Task TypeAsync(ComboboxTestContext context, IRenderedComponent<AutocompleteHarness> cut, string text)
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = context.TwinOf(ComboboxTwin.InputPart);
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }
}
