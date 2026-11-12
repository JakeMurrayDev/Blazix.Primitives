using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196): Autocomplete's form value, Clear, placeholder and selection titles at L2 over the engine's None
// paths (AUT-DOM-01, 02, 05, API-13, 14, STA-01..03, EDGE-02; AutocompleteRoot.test.tsx, AutocompleteItem.test.tsx,
// ComboboxClear.test.tsx's no-selection title). Native FormData submissions are L4's.
/// <summary>Autocomplete's form value and its part attributes at L2.</summary>
public sealed class AutocompleteFormTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should pass autoComplete to the visible input")]
    [Trait("Requirement", "AUT-API-14")]
    [Trait("Requirement", "AUT-DOM-01")]
    [Trait("Requirement", "AUT-DOM-02")]
    public void Passes_auto_complete_to_the_visible_input()
    {
        IRenderedComponent<AutocompleteFormHost> cut = Render(new() { ["Items"] = s_items, ["Name"] = "tag" }, new() { ["autocomplete"] = "email" });

        IElement visible = cut.Find("[data-testid=input]");
        Assert.Equal("email", visible.GetAttribute("autocomplete"));
        Assert.Equal("tag", visible.GetAttribute("name"));
        // The hidden input's name and id follow the Input's registration after its first render; the nameless hidden input
        // is asserted through native FormData at L4 ("submits the typed input value when name is provided on Autocomplete.Root").
        Assert.False(cut.Find("input[aria-hidden=true]").HasAttribute("autocomplete"));
    }

    [Fact(DisplayName = "gives form value ownership to the hidden input when the input is inside the popup")]
    [Trait("Requirement", "AUT-DOM-02")]
    [Trait("Requirement", "AUT-API-13")]
    public async Task Gives_the_hidden_input_the_form_value_when_the_input_is_inside_the_popup()
    {
        IRenderedComponent<AutocompleteFormHost> cut = Render(new() { ["Items"] = s_items, ["Name"] = "tag", ["DefaultOpen"] = true, ["DefaultValue"] = "ch" }, insidePopup: true);

        IElement hidden = cut.Find("input[aria-hidden=true]");
        Assert.Equal("tag", hidden.GetAttribute("name"));
        Assert.Equal("ch", hidden.GetAttribute("value"));
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("name"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-cherry]").Click());

        // The hidden input's value is the form module's to write after the first render (C# -> JS, autocomplete.md §5.5).
        Assert.Equal("cherry", cut.Instance.Root!.Store.State.InputValue);
        Assert.Contains("cherry", (string)test.FormHandle.Invocations["update"][^1].Arguments[0]!, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "sets `required` on the visible input")]
    [Trait("Requirement", "AUT-DOM-01")]
    public void Sets_required_on_the_visible_input()
    {
        IRenderedComponent<AutocompleteFormHost> cut = Render(new() { ["Items"] = s_items, ["Required"] = true });

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("required"));
    }

    [Fact(DisplayName = "treats a controlled null value as an empty query")]
    [Trait("Requirement", "AUT-EDGE-02")]
    public void Treats_a_controlled_null_value_as_an_empty_query()
    {
        IRenderedComponent<AutocompleteFormHost> cut = Render(new() { ["Items"] = s_items, ["Value"] = null, ["DefaultOpen"] = true });

        Assert.Equal(string.Empty, cut.Instance.Root!.Store.State.InputValue);
        Assert.Equal(3, cut.FindAll("[role=option]").Count);
    }

    [Fact(DisplayName = "does not expose data-placeholder on Trigger or InputGroup")]
    [Trait("Requirement", "AUT-STA-01")]
    [Trait("Requirement", "AUT-DOM-04")]
    public async Task Does_not_expose_data_placeholder()
    {
        IRenderedComponent<AutocompleteFormHost> cut = Render(new() { ["Items"] = s_items });
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-placeholder"));
        Assert.False(cut.Find("[data-testid=group]").HasAttribute("data-placeholder"));

        await TypeAsync(cut, "ap");

        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-placeholder"));
        Assert.False(cut.Find("[data-testid=group]").HasAttribute("data-placeholder"));
    }

    [Fact(DisplayName = "does not expose data-selected when reopening after a value is chosen")]
    [Trait("Requirement", "AUT-STA-03")]
    [Trait("Requirement", "AUT-DOM-03")]
    public async Task Does_not_expose_data_selected_after_a_value_is_chosen()
    {
        IRenderedComponent<AutocompleteFormHost> cut = Render(new() { ["Items"] = s_items, ["DefaultOpen"] = true });
        await cut.InvokeAsync(() => cut.Find("[data-testid=item-apple]").Click());
        Assert.Equal("apple", cut.Instance.Root!.Store.State.InputValue);

        string reopen = $$"""{"open":true,"reason":"trigger-press","event":{{ComboboxTestContext.Click}},"triggerId":null}""";
        InteractionsTwin session = test.SessionTwin;
        await cut.InvokeAsync(() => session.OnRequest(reopen));

        Assert.NotEmpty(cut.FindAll("[role=option]"));
        Assert.All(cut.FindAll("[role=option]"), item => Assert.False(item.HasAttribute("data-selected")));
        Assert.False(cut.Instance.Root!.Store.State.HasSelectedValue);
    }

    [Fact(DisplayName = "renders and clears the typed value in no-selection mode")]
    [Trait("Requirement", "AUT-STA-02")]
    [Trait("Requirement", "AUT-DOM-05")]
    public async Task Renders_and_clears_the_typed_value()
    {
        List<(string Text, EventReason Reason)> changes = [];
        IRenderedComponent<AutocompleteFormHost> cut = Render(new()
        {
            ["Items"] = s_items,
            ["OnValueChange"] = EventCallback.Factory.Create<ValueChangeEventArgs<string>>(this, args => changes.Add((args.Value, args.Details.Reason))),
        });
        Assert.Empty(cut.FindAll("[data-testid=clear]"));

        await TypeAsync(cut, "ba");
        IElement clear = cut.Find("[data-testid=clear]");
        Assert.True(clear.HasAttribute("data-visible"));

        await cut.InvokeAsync(() => clear.Click());

        Assert.Equal(string.Empty, cut.Instance.Root!.Store.State.InputValue);
        Assert.Equal([("ba", EventReason.InputChange), (string.Empty, EventReason.ClearPress)], changes);
    }

    private IRenderedComponent<AutocompleteFormHost> Render(Dictionary<string, object?> root, Dictionary<string, object>? inputAttributes = null, bool insidePopup = false) =>
        test.Context.Render<AutocompleteFormHost>(p => p
            .Add(x => x.RootParameters, root)
            .Add(x => x.InputAttributes, inputAttributes ?? [])
            .Add(x => x.InsidePopup, insidePopup));

    private Task TypeAsync(IRenderedComponent<AutocompleteFormHost> cut, string text)
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }
}
