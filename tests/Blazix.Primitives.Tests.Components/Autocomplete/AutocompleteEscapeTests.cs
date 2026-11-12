using Blazix.Primitives.Interop;
using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M5 track 11 (issue 196): the closed Escape over None mode's internal value (AUT-KEY-07, STA-08; ComboboxInput.tsx:351-364).
// The engine keeps the last activated item's value; the closed Escape stops when it is set and the root is not inline, then
// resets it (design point 1 (ii), settled by the orchestrator 2026-11-12). The Input twin's `clear` decision is called as
// the module calls it.
/// <summary>Autocomplete's closed Escape at L2.</summary>
public sealed class AutocompleteEscapeTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = AutocompleteTesting.CreateContext();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: after an item is activated the closed Escape clears the text and stops, and the next closed Escape does not stop")]
    [Trait("Requirement", "AUT-KEY-07")]
    [Trait("Requirement", "AUT-STA-08")]
    public async Task After_an_item_is_activated_the_closed_escape_stops_once()
    {
        IRenderedComponent<AutocompleteHarness> cut = test.Context.Render<AutocompleteHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = s_items, ["DefaultOpen"] = true }));
        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());
        Assert.Equal("banana", cut.Instance.Root!.Store.State.InputValue);
        Assert.Contains("\"hasValue\":true", test.LastInputOptions(), StringComparison.Ordinal);
        ComboboxTwin input = test.TwinOf(ComboboxTwin.InputPart);

        string first = await cut.InvokeAsync(() => input.Decide(ComboboxTwin.ClearPoint, ComboboxTestContext.Key("Escape")));
        string second = await cut.InvokeAsync(() => input.Decide(ComboboxTwin.ClearPoint, ComboboxTestContext.Key("Escape")));

        Assert.Equal("stop", first);
        Assert.Equal("run", second);
        Assert.Equal(string.Empty, cut.Instance.Root!.Store.State.InputValue);
        Assert.Contains((string.Empty, EventReason.EscapeKey), cut.Instance.ValueChanges);
        Assert.False(cut.Instance.Root!.Store.State.HasSelectedValue);
        Assert.Contains("\"hasValue\":false", test.LastInputOptions(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: the closed Escape does not stop before any item was activated, and never while inline")]
    [Trait("Requirement", "AUT-KEY-07")]
    public async Task The_closed_escape_runs_before_an_activation_and_inline()
    {
        IRenderedComponent<AutocompleteHarness> cut = test.Context.Render<AutocompleteHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = s_items, ["DefaultValue"] = "ap" }));
        ComboboxTwin input = test.TwinOf(ComboboxTwin.InputPart);

        Assert.Equal("run", await cut.InvokeAsync(() => input.Decide(ComboboxTwin.ClearPoint, ComboboxTestContext.Key("Escape"))));
        Assert.Equal(string.Empty, cut.Instance.Root!.Store.State.InputValue);

        using ComboboxTestContext inlineTest = AutocompleteTesting.CreateContext();
        IRenderedComponent<AutocompleteHarness> inline = inlineTest.Context.Render<AutocompleteHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = s_items, ["Inline"] = true, ["Open"] = true }));
        await inline.InvokeAsync(() => inline.Find("[data-testid=item-apple]").Click());
        ComboboxTwin inlineInput = inlineTest.TwinOf(ComboboxTwin.InputPart);
        Assert.Equal("run", await inline.InvokeAsync(() => inlineInput.Decide(ComboboxTwin.ClearPoint, ComboboxTestContext.Key("Escape"))));
    }
}
