using System.Globalization;
using Blazix.Primitives.Interop;
using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196): Autocomplete's filtering over object items and the default filter's culture at L2 (AUT-API-08,
// API-10). Port note (the brief's Deliverable 6): an item's `label` or `value` member is never read (ADR 16 rule 1, no
// reflection; track 4's design point 2), so Base UI's `{label}` and `{value}` object titles are ported with
// ItemToStringValue, a dated deviation on API-08's fallback clause.
/// <summary>Autocomplete's filtering of object items and its culture at L2.</summary>
public sealed class AutocompleteFilteringTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: filters and displays using itemToStringValue for object items, and fills the input with it")]
    [Trait("Requirement", "AUT-API-08")]
    public async Task Filters_and_fills_with_item_to_string_value()
    {
        IReadOnlyList<Tag> items = [new("t1", "feature"), new("t2", "fix"), new("t3", "bug")];
        IRenderedComponent<AutocompleteObjectHost<Tag>> cut = test.Context.Render<AutocompleteObjectHost<Tag>>(p => p
            .Add(x => x.Text, static tag => tag.Value)
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Items"] = items,
                ["DefaultOpen"] = true,
                ["ItemToStringValue"] = (Func<Tag, string>)(static tag => tag.Value),
            }));

        await TypeAsync(cut, "f");
        Assert.Equal(["feature", "fix"], cut.FindAll("[role=option]").Select(item => item.TextContent));

        await cut.InvokeAsync(() => cut.FindAll("[role=option]")[1].Click());
        Assert.Equal("fix", cut.Instance.Root!.Store.State.InputValue);
    }

    [Fact(DisplayName = "mode=\"list\": uses the locale when applying the default filter")]
    [Trait("Requirement", "AUT-API-10")]
    public async Task Uses_the_culture_when_applying_the_default_filter()
    {
        IRenderedComponent<AutocompleteObjectHost<string>> cut = test.Context.Render<AutocompleteObjectHost<string>>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Items"] = (IReadOnlyList<string>)["İzmir", "Isparta"],
                ["DefaultOpen"] = true,
                ["Culture"] = new CultureInfo("tr-TR"),
            }));

        await TypeAsync(cut, "i");

        Assert.Equal(["İzmir"], cut.FindAll("[role=option]").Select(item => item.TextContent));
    }

    private Task TypeAsync<TItem>(IRenderedComponent<AutocompleteObjectHost<TItem>> cut, string text)
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }

    private sealed record Tag(string Id, string Value);
}
