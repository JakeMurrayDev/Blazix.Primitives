using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §3.1, §4 at L2 (ComboboxRoot.test.tsx's titles whose assertions are what C# renders and sends): the ARIA the input
// renders, the initial input text, the controlled values and the selection's request sequence. A typed change reaches the
// store as the `combobox` module reports it on Server (ComboboxTestContext.TypeAsync); the input's live `value` is the
// module's (ADR 04 Amendments 2026-11-03 (6)), so these titles assert the rendered value and the committed text sent.
/// <summary>Combobox's root at L2: the store core.</summary>
public sealed class ComboboxRootTests : IDisposable
{
    private static readonly string[] s_fruits = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "sets all aria attributes on the input when closed")]
    [Trait("Requirement", "CMB-STR-01")]
    [Trait("Requirement", "CMB-ARIA-01")]
    public void Sets_all_aria_attributes_on_the_input_when_closed()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new());
        IElement input = cut.Find("[data-testid=input]");

        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("false", input.GetAttribute("aria-expanded"));
        Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
        Assert.Equal("listbox", input.GetAttribute("aria-haspopup"));
        Assert.False(input.HasAttribute("aria-controls"));
        Assert.False(input.HasAttribute("aria-activedescendant"));

        // CMB-STR-01: no wrapper element: the input's parent is the test's container.
        Assert.Equal("input", cut.Nodes.OfType<IElement>().First().TagName.ToLowerInvariant());
    }

    [Fact(DisplayName = "derives input from defaultValue on first mount when unspecified")]
    [Trait("Requirement", "CMB-STA-19")]
    [Trait("Requirement", "CMB-API-02")]
    public void Derives_input_from_defaultValue_on_first_mount_when_unspecified()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultValue"] = "apple" });

        Assert.Equal("apple", cut.Find("[data-testid=input]").GetAttribute("value"));
    }

    [Fact(DisplayName = "defaultInputValue overrides derivation when provided")]
    [Trait("Requirement", "CMB-STA-19")]
    [Trait("Requirement", "CMB-API-10")]
    public void DefaultInputValue_overrides_derivation_when_provided()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultValue"] = "apple", ["DefaultInputValue"] = "x" });

        Assert.Equal("x", cut.Find("[data-testid=input]").GetAttribute("value"));
    }

    [Fact(DisplayName = "multiple mode initial input remains empty")]
    [Trait("Requirement", "CMB-STA-19")]
    [Trait("Requirement", "CMB-API-02")]
    public void Multiple_mode_initial_input_remains_empty()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Multiple"] = true, ["Items"] = (IReadOnlyList<string>)s_fruits });

        Assert.Equal(string.Empty, cut.Find("[data-testid=input]").GetAttribute("value"));
    }

    [Fact(DisplayName = "does not set input value for input-inside-popup pattern")]
    [Trait("Requirement", "CMB-STA-19")]
    public void Does_not_set_input_value_for_input_inside_popup_pattern()
    {
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true, ["DefaultValue"] = "apple" })
            .Add(x => x.InputInsidePopup, true));

        Assert.Equal(string.Empty, cut.Instance.Root!.Store.State.InputValue);
        Assert.Contains(cut.Instance.InputChanges, change => change.Text.Length == 0 && change.Reason == EventReason.None);
    }

    [Fact(DisplayName = "normalizes a controlled null value when selecting in multiple mode")]
    [Trait("Requirement", "CMB-API-01")]
    [Trait("Requirement", "CMB-EDGE-11")]
    public async Task Normalizes_a_controlled_null_value_when_selecting_in_multiple_mode()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(
            new() { ["Multiple"] = true, ["Values"] = null, ["DefaultOpen"] = true },
            staticItems: ["apple"]);

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-apple]").Click());

        (IReadOnlyList<string> values, EventReason reason) = Assert.Single(cut.Instance.ValuesChanges);
        Assert.Equal(["apple"], values);
        Assert.Equal(EventReason.ItemPress, reason);
    }

    [Fact(DisplayName = "\"single\" keeps the popup open when selection is canceled")]
    [Trait("Requirement", "CMB-API-03")]
    [Trait("Requirement", "CMB-STA-18")]
    [Trait("Requirement", "CMB-EVT-07")]
    public async Task Single_keeps_the_popup_open_when_selection_is_canceled()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true, ["Items"] = (IReadOnlyList<string>)s_fruits });
        cut.Instance.OnValue = args => args.Details.Cancel();

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());

        Assert.True(cut.Instance.Root!.Store.IsOpen);
        Assert.False(cut.Instance.Root.Store.State.HasSelectedValue);
        Assert.Empty(cut.Instance.OpenChanges);
        Assert.Empty(cut.Instance.InputChanges);
    }

    [Fact(DisplayName = "port: a single selection reports the value, then the input's label, then the close, each with item-press")]
    [Trait("Requirement", "CMB-EVT-10")]
    [Trait("Requirement", "CMB-EVT-04")]
    public async Task A_single_selection_reports_the_value_then_the_label_then_the_close()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true, ["Items"] = (IReadOnlyList<string>)s_fruits });
        List<string> order = [];
        cut.Instance.OnValue = args => order.Add("value:" + args.Value);
        cut.Instance.OnInput = args => order.Add("input:" + args.Value);
        cut.Instance.OnOpen = args => order.Add("open:" + args.Value);

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());

        Assert.Equal(["value:banana", "input:banana", "open:False"], order);
        Assert.All(cut.Instance.OpenChanges, change => Assert.Equal(EventReason.ItemPress, change.Reason));
        Assert.Equal("banana", cut.Instance.Root!.Store.State.InputValue);
        Assert.False(cut.Instance.Root.Store.IsOpen);
    }

    [Fact(DisplayName = "port: typing commits the text and sends it to the input's handle with a new revision")]
    [Trait("Requirement", "CMB-API-09")]
    [Trait("Requirement", "CMB-API-11")]
    [Trait("Requirement", "CMB-STA-21")]
    public async Task Typing_commits_the_text_and_sends_it_with_a_new_revision()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits });

        await test.TypeAsync(cut, "ba");

        (string text, EventReason reason, _) = Assert.Single(cut.Instance.InputChanges);
        Assert.Equal("ba", text);
        Assert.Equal(EventReason.InputChange, reason);
        Assert.Contains("\"value\":\"ba\"", test.LastInputOptions(), StringComparison.Ordinal);
        Assert.Equal(["banana"], cut.Instance.Root!.Store.FilteredValues);

        // ComboboxInput.tsx:266-275: a typed non-blank change opens the popup with input-change.
        Assert.True(cut.Instance.Root.Store.IsOpen);
        Assert.Contains(cut.Instance.OpenChanges, change => change.Open && change.Reason == EventReason.InputChange);
    }

    [Fact(DisplayName = "port: an autofill-like change commits the text without opening the popup")]
    [Trait("Requirement", "CMB-KEY-17")]
    [Trait("Requirement", "CMB-STA-21")]
    public async Task An_autofill_like_change_commits_the_text_without_opening()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits });

        await test.TypeAsync(cut, "cherry", typed: false);

        Assert.Equal("cherry", cut.Instance.Root!.Store.State.InputValue);
        Assert.False(cut.Instance.Root.Store.IsOpen);
    }

    [Fact(DisplayName = "port: emptying the input outside the popup clears the single selection with input-clear")]
    [Trait("Requirement", "CMB-EVT-03")]
    public async Task Emptying_the_input_clears_the_single_selection()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultValue"] = "apple", ["Items"] = (IReadOnlyList<string>)s_fruits });

        await test.TypeAsync(cut, string.Empty);

        (string? value, EventReason reason) = Assert.Single(cut.Instance.ValueChanges);
        Assert.Null(value);
        Assert.Equal(EventReason.InputClear, reason);
        Assert.False(cut.Instance.Root!.Store.State.HasSelectedValue);
    }

    [Fact(DisplayName = "port: a part outside the root throws the missing-root error with the family's names")]
    [Trait("Requirement", "CMB-STR-02")]
    public void A_part_outside_the_root_throws_the_missing_root_error()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<ComboboxInput>());

        Assert.Equal("Blazix: `ComboboxRootContext` is missing. Combobox parts must be placed within `<ComboboxRoot>`.", error.Message);
    }

    [Fact(DisplayName = "port: the item of a default value renders selected on the first render")]
    [Trait("Requirement", "CMB-ARIA-09")]
    public void The_item_of_a_default_value_renders_selected_on_the_first_render()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true, ["DefaultValue"] = "banana" }, staticItems: s_fruits);

        Assert.Equal("true", cut.Find("[data-testid=item-banana]").GetAttribute("aria-selected"));
        Assert.Equal("false", cut.Find("[data-testid=item-apple]").GetAttribute("aria-selected"));
    }

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> parameters, IReadOnlyList<string>? staticItems = null) =>
        test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, parameters).Add(x => x.StaticItems, staticItems));
}
