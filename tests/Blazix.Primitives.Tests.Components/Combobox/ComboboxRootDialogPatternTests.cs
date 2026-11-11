using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-STA-26 at L2 (ComboboxRoot.test.tsx, describe "dialog pattern"): an inline combobox whose `open` follows a kept-mounted
// Dialog clears its highlight when the dialog closes, so a reopen starts without one. The Dialog's Trigger, Close and Escape
// end in its onOpenChange's setOpen (ComboboxDialogHost.SetOpen); Base UI runs with animations disabled, so each close ends at
// once (CompleteCloseAsync). The highlight is the store's active index (ComboboxRootBatchCSteps).
/// <summary>ComboboxRoot inline in a Dialog, at L2.</summary>
public sealed class ComboboxRootDialogPatternTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Theory(DisplayName = "clears the highlight when a keepMounted dialog closes (multiple=%s, query=\"%s\")")]
    [InlineData(false, "")]
    [InlineData(false, "Ba")]
    [InlineData(true, "")]
    [InlineData(true, "Ba")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Clears_the_highlight_when_a_keepMounted_dialog_closes(bool multiple, string query)
    {
        IRenderedComponent<ComboboxDialogHost> cut = test.Context.Render<ComboboxDialogHost>(p => p
            .Add(x => x.Items, ["Apple", "Banana"])
            .Add(x => x.Multiple, multiple)
            .Add(x => x.KeepMounted, true)
            .Add(x => x.CloseOnValueChange, true));
        ComboboxStore store = cut.Instance.Root!.Store;
        await SetOpenAsync(cut, true);
        if (query.Length != 0)
        {
            await test.TypeEachAsync(cut, query);
            await test.HighlightAsync(cut, 0, ComboboxRootBatchCSteps.ArrowDown);
        }
        else
        {
            await test.HighlightAsync(cut, 0, ComboboxRootBatchCSteps.ArrowDown);
            await test.HighlightAsync(cut, 1, ComboboxRootBatchCSteps.ArrowDown);
        }

        Assert.Equal("Banana", cut.HighlightedText(store));
        cut.Instance.Highlights.Clear();

        await test.EnterAsync(cut, store);
        await test.CompleteCloseAsync(cut, store);

        Assert.False(cut.Instance.IsOpen);
        object? last = cut.Instance.ValueChanges[^1];
        if (multiple)
        {
            Assert.Equal(["Banana"], Assert.IsAssignableFrom<IReadOnlyList<string>>(last));
        }
        else
        {
            Assert.Equal("Banana", last);
        }

        Assert.Equal([null], cut.Instance.Highlights);

        await ReopenAndNavigateAsync(cut, store);
        await ReopenAndNavigateAsync(cut, store);
        await ReopenAndNavigateAsync(cut, store);
    }

    // Each reopen starts without a highlight, Enter selects nothing, ArrowDown highlights, and Escape or Done closes: both
    // are the Dialog's setOpen(false).
    private async Task ReopenAndNavigateAsync(IRenderedComponent<ComboboxDialogHost> cut, ComboboxStore store)
    {
        await SetOpenAsync(cut, true);
        Assert.Null(store.State.ActiveIndex);
        cut.Instance.ValueChanges.Clear();

        await test.EnterAsync(cut, store);
        Assert.Empty(cut.Instance.ValueChanges);

        await test.HighlightAsync(cut, 0, ComboboxRootBatchCSteps.ArrowDown);
        await SetOpenAsync(cut, false);
        await test.CompleteCloseAsync(cut, store);
        Assert.False(cut.Instance.IsOpen);
    }

    private async Task SetOpenAsync(IRenderedComponent<ComboboxDialogHost> cut, bool open)
    {
        await cut.InvokeAsync(() => cut.Instance.SetOpen(open));
        await test.EchoAsync(cut);
    }
}
