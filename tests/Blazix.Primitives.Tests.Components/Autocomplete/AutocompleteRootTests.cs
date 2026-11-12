using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196): Autocomplete's Root, Value and shells at L2 over the engine in None mode (AUT-STR-03), with the
// combobox module's handles faked by ComboboxTestContext. The Input's text is typed as the module reports a typed change.
/// <summary>Autocomplete's Root, Value and shells at L2.</summary>
public sealed class AutocompleteRootTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the parts render their elements inside a root, the input as the combobox with aria-autocomplete list")]
    [Trait("Requirement", "AUT-STR-01")]
    [Trait("Requirement", "AUT-DOM-03")]
    [Trait("Requirement", "AUT-ARIA-01")]
    public void The_parts_render_their_elements_inside_a_root()
    {
        IRenderedComponent<AutocompleteHarness> cut = Render(new() { ["Items"] = s_items, ["DefaultOpen"] = true, ["DefaultValue"] = "a" });

        IElement input = cut.Find("[data-testid=input]");
        Assert.Equal("input", input.TagName.ToLowerInvariant());
        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
        Assert.Equal("button", cut.Find("[data-testid=trigger]").TagName.ToLowerInvariant());
        Assert.Equal("button", cut.Find("[data-testid=clear]").TagName.ToLowerInvariant());
        Assert.Equal("listbox", cut.Find("[data-testid=list]").GetAttribute("role"));
        Assert.Equal(["apple", "banana"], cut.FindAll("[role=option]").Select(item => item.TextContent));
        Assert.Equal("presentation", cut.Find("[data-testid=separator]").GetAttribute("role"));
        Assert.All(cut.FindAll("[role=option]"), item => Assert.False(item.HasAttribute("data-selected")));
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-placeholder"));
        Assert.False(cut.Find("[data-testid=group]").HasAttribute("data-placeholder"));
        Assert.NotEmpty(cut.Instance.InputStates);
        Assert.NotEmpty(cut.Instance.ItemStates);
    }

    [Fact(DisplayName = "port: aria-autocomplete follows Mode at run time, and is none while read-only")]
    [Trait("Requirement", "AUT-ARIA-01")]
    [Trait("Requirement", "AUT-API-02")]
    [Trait("Requirement", "AUT-API-17")]
    public void Aria_autocomplete_follows_mode_at_run_time()
    {
        Dictionary<string, object?> parameters = new() { ["Items"] = s_items, ["Mode"] = AutocompleteMode.Both };
        IRenderedComponent<AutocompleteHarness> cut = Render(parameters);
        Assert.Equal("both", cut.Find("[data-testid=input]").GetAttribute("aria-autocomplete"));

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?>(parameters) { ["Mode"] = AutocompleteMode.Inline }));
        Assert.Equal("inline", cut.Find("[data-testid=input]").GetAttribute("aria-autocomplete"));

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?>(parameters) { ["Mode"] = AutocompleteMode.None }));
        Assert.Equal("none", cut.Find("[data-testid=input]").GetAttribute("aria-autocomplete"));

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?>(parameters) { ["ReadOnly"] = true }));
        Assert.Equal("none", cut.Find("[data-testid=input]").GetAttribute("aria-autocomplete"));
    }

    [Fact(DisplayName = "port: an Autocomplete part outside a root throws an error that names AutocompleteRoot")]
    [Trait("Requirement", "AUT-STR-04")]
    public void A_part_outside_a_root_throws_an_error_that_names_autocomplete_root()
    {
        InvalidOperationException error = Assert.ThrowsAny<InvalidOperationException>(() => test.Context.Render<AutocompleteInput>());

        Assert.StartsWith("Blazix: ", error.Message, StringComparison.Ordinal);
        Assert.Contains("AutocompleteRoot", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ComboboxRoot", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: AutocompleteValue renders the text, its template and its content, and an empty text outside a root")]
    [Trait("Requirement", "AUT-API-18")]
    [Trait("Requirement", "AUT-STR-04")]
    public async Task Value_renders_the_text_its_template_and_its_content()
    {
        IRenderedComponent<AutocompleteHarness> cut = Render(new() { ["Items"] = s_items, ["DefaultValue"] = "ap" });
        Assert.Equal("ap", cut.Find("[data-testid=trigger]").TextContent);

        await TypeAsync(cut, "app");
        Assert.Equal("app", cut.Find("[data-testid=trigger]").TextContent);

        RenderFragment<string> template = text => builder => builder.AddContent(0, "[" + text + "]");
        Assert.Equal("[]", test.Context.Render<AutocompleteValue>(p => p.Add(x => x.TextContent, template)).Markup);
        Assert.Equal("fixed", test.Context.Render<AutocompleteValue>(p => p.AddChildContent("fixed")).Markup);
        Assert.Equal(string.Empty, test.Context.Render<AutocompleteValue>().Markup);
    }

    [Fact(DisplayName = "port: typing commits the text and reports it to OnValueChange with the input-change reason")]
    [Trait("Requirement", "AUT-API-01")]
    [Trait("Requirement", "AUT-API-03")]
    public async Task Typing_commits_the_text_and_reports_it()
    {
        IRenderedComponent<AutocompleteHarness> cut = Render(new() { ["Items"] = s_items });

        await TypeAsync(cut, "ban");

        Assert.Equal([("ban", EventReason.InputChange)], cut.Instance.ValueChanges);
        Assert.Contains("\"value\":\"ban\"", test.LastInputOptions(), StringComparison.Ordinal);
        Assert.Equal(["banana"], cut.FindAll("[role=option]").Select(item => item.TextContent));
    }

    [Fact(DisplayName = "port: the inline and none modes and a null filter show every item, and the list mode filters by the text")]
    [Trait("Requirement", "AUT-API-09")]
    [Trait("Requirement", "AUT-STA-07")]
    public async Task The_static_modes_and_a_null_filter_show_every_item()
    {
        foreach ((AutocompleteMode mode, bool nullFilter, int expected) in new[]
        {
            (AutocompleteMode.Inline, false, 3), (AutocompleteMode.None, false, 3), (AutocompleteMode.List, true, 3),
            (AutocompleteMode.Both, true, 3), (AutocompleteMode.List, false, 1), (AutocompleteMode.Both, false, 1),
        })
        {
            Dictionary<string, object?> parameters = new() { ["Items"] = s_items, ["DefaultOpen"] = true, ["Mode"] = mode };
            if (nullFilter)
            {
                parameters["Filter"] = null;
            }

            using ComboboxTestContext context = new();
            IRenderedComponent<AutocompleteHarness> cut = context.Context.Render<AutocompleteHarness>(p => p.Add(x => x.RootParameters, parameters));
            await TypeAsync(context, cut, "cher");

            Assert.True(expected == cut.FindAll("[role=option]").Count, $"{mode}, null filter {nullFilter}: {cut.FindAll("[role=option]").Count} items");
        }
    }

    [Fact(DisplayName = "mode=\"list\": uses a custom filter instead of the locale-aware default")]
    [Trait("Requirement", "AUT-API-09")]
    public async Task Uses_a_custom_filter_instead_of_the_default()
    {
        Func<string, string, Func<string, string>?, bool> startsWithLastLetter = (item, query, _) => query.Length == 0 || item.EndsWith(query[^1]);
        IRenderedComponent<AutocompleteHarness> cut = Render(new() { ["Items"] = s_items, ["DefaultOpen"] = true, ["Filter"] = startsWithLastLetter });

        await TypeAsync(cut, "xa");

        Assert.Equal(["banana"], cut.FindAll("[role=option]").Select(item => item.TextContent));
    }

    [Fact(DisplayName = "sets aria-readonly on the Autocomplete listbox when the root is readOnly")]
    [Trait("Requirement", "AUT-ARIA-02")]
    public void Sets_aria_readonly_on_the_listbox_when_read_only()
    {
        IRenderedComponent<AutocompleteHarness> cut = Render(new() { ["Items"] = s_items, ["DefaultOpen"] = true, ["ReadOnly"] = true });

        Assert.Equal("true", cut.Find("[data-testid=list]").GetAttribute("aria-readonly"));
        Assert.All(cut.FindAll("[role=option]"), item => Assert.False(item.HasAttribute("aria-selected") && item.GetAttribute("aria-selected") == "true"));
    }

    [Fact(DisplayName = "exposes the listbox separator behavior")]
    [Trait("Requirement", "AUT-STR-05")]
    public void Exposes_the_listbox_separator_behavior()
    {
        IRenderedComponent<AutocompleteHarness> cut = Render(new() { ["Items"] = s_items, ["DefaultOpen"] = true });

        IElement separator = cut.Find("[data-testid=separator]");
        Assert.Equal("presentation", separator.GetAttribute("role"));
        Assert.Equal("div", separator.TagName.ToLowerInvariant());
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
