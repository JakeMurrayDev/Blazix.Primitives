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
