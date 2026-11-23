using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx, describe "coverage edge cases", at L2: a canceled open, a close after a cleared query, the multiple
// inline query cleared on close, a controlled null in multiple mode, the inline initial selected index (the test's
// SelectedIndexProbe reads the store's selected index), and the unmount action while open. The title of line 13964
// renders Autocomplete.Root with autoHighlight="always", a mode Combobox.Root does not expose (§16 Q12), so it has no test
// here.
/// <summary>ComboboxRoot's coverage edge cases at L2.</summary>
public sealed class ComboboxRootCoverageEdgeCasesTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "allows an attempted open to be canceled")]
    [Trait("Requirement", "CMB-STA-18")]
    [Trait("Requirement", "CMB-EVT-07")]
    public async Task Allows_an_attempted_open_to_be_canceled()
    {
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p.Add(x => x.StaticItems, []));
        cut.Instance.OnOpen = args => args.Details.Cancel();

        await test.PressAsync(cut, "[data-testid=input]", "input-press");

        Assert.Contains(cut.Instance.OpenChanges, change => change.Open);
        Assert.False(cut.Instance.Root!.Store.IsOpen);
        Assert.Empty(cut.FindAll("[role=listbox]"));
    }

    [Fact(DisplayName = "handles closing after a changed query is cleared")]
    [Trait("Requirement", "CMB-STA-21")]
    public async Task Handles_closing_after_a_changed_query_is_cleared()
    {
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true, ["Items"] = (IReadOnlyList<string>)["apple"] }));
        ComboboxStore store = cut.Instance.Root!.Store;

        await test.TypeEachAsync(cut, "a");
        await test.InputAsync(cut, string.Empty);
        await test.DismissAsync(cut, "escape-key");
        await test.CompleteCloseAsync(cut, store);

        Assert.Empty(cut.FindAll("[role=listbox]"));
    }

    [Fact(DisplayName = "clears a multiple inline query on close")]
    [Trait("Requirement", "CMB-STA-30")]
    public async Task Clears_a_multiple_inline_query_on_close()
    {
        IRenderedComponent<ComboboxTypedHarness<string>> cut = test.Context.Render<ComboboxTypedHarness<string>>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Multiple"] = true, ["Inline"] = true, ["Open"] = true, ["Items"] = (IReadOnlyList<string>)["apple"] })
            .Add(x => x.InlineList, true)
            .Add(x => x.WithTrigger, true));
        await test.TypeEachAsync(cut, "a");

        await test.PressAsync(cut, "[data-testid=trigger]", "trigger-press", open: false);

        Assert.Equal(string.Empty, cut.InputText());
    }

    [Fact(DisplayName = "normalizes a controlled null value while closed in multiple mode")]
    [Trait("Requirement", "CMB-API-01")]
    [Trait("Requirement", "CMB-EDGE-11")]
    public void Normalizes_a_controlled_null_value_while_closed_in_multiple_mode()
    {
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Multiple"] = true, ["Values"] = null, ["Items"] = (IReadOnlyList<string>)["apple"] }));

        Assert.Null(cut.Instance.Root!.Store.State.SelectedIndex);
    }

    [Fact(DisplayName = "does not seed an initial highlight for an unmatched inline value")]
    [Trait("Requirement", "CMB-API-23")]
    [Trait("Requirement", "CMB-STA-28")]
    public void Does_not_seed_an_initial_highlight_for_an_unmatched_inline_value()
    {
        IRenderedComponent<ComboboxTypedHarness<string>> cut = RenderInline("missing");

        Assert.Null(cut.Instance.Root!.Store.State.SelectedIndex);
    }

    [Fact(DisplayName = "seeds the initial highlight for a matched inline value")]
    [Trait("Requirement", "CMB-API-23")]
    [Trait("Requirement", "CMB-STA-28")]
    public void Seeds_the_initial_highlight_for_a_matched_inline_value()
    {
        IRenderedComponent<ComboboxTypedHarness<string>> cut = RenderInline("apple");

        Assert.Equal(0, cut.Instance.Root!.Store.State.SelectedIndex);
    }

    [Fact(DisplayName = "exposes an unmount action that is ignored while open")]
    [Trait("Requirement", "CMB-API-36")]
    public async Task Exposes_an_unmount_action_that_is_ignored_while_open()
    {
        List<bool> completions = [];
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["DefaultOpen"] = true,
                ["OnOpenChangeComplete"] = EventCallback.Factory.Create<bool>(this, completions.Add),
            })
            .Add(x => x.KeepMounted, false));

        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());

        Assert.DoesNotContain(false, completions);
        Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-expanded"));
    }

    private IRenderedComponent<ComboboxTypedHarness<string>> RenderInline(string defaultValue) =>
        test.Context.Render<ComboboxTypedHarness<string>>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Inline"] = true,
                ["Open"] = true,
                ["DefaultValue"] = defaultValue,
                ["Items"] = (IReadOnlyList<string>)["apple"],
            })
            .Add(x => x.InlineList, true));
}
