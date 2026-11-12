using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-API-23 and STA-28 at L2 (ComboboxRoot.test.tsx, describe "dialog pattern > single"): an inline Input saves its highlight
// when focus leaves it, clears it, and restores it when focus returns within the same open cycle (ComboboxInput.tsx:188-216,
// the Input's own onBlur and onFocus). Base UI fires the focus events itself; here they are the Input's Blazor events.
/// <summary>ComboboxRoot inline in a Dialog, single selection, at L2.</summary>
public sealed class ComboboxRootDialogPatternSingleTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "restores highlight when the input regains focus")]
    [Trait("Requirement", "CMB-API-23")]
    [Trait("Requirement", "CMB-STA-28")]
    public async Task Restores_highlight_when_the_input_regains_focus()
    {
        IRenderedComponent<ComboboxDialogHost> cut = test.Context.Render<ComboboxDialogHost>(p => p
            .Add(x => x.Items, ["Apple", "Apricot", "Banana", "Grape", "Orange"])
            .Add(x => x.DefaultOpen, true));
        ComboboxStore store = cut.Instance.Root!.Store;
        await cut.InvokeAsync(() => cut.Find("[data-testid=dialog-input]").Focus());

        await test.HighlightAsync(cut, 0, ComboboxRootBatchCSteps.ArrowDown);
        Assert.Equal("Apple", cut.HighlightedText(store));

        await cut.InvokeAsync(() => cut.Find("[data-testid=dialog-input]").Blur());
        await test.EchoAsync(cut);

        Assert.Null(store.State.ActiveIndex);

        await cut.InvokeAsync(() => cut.Find("[data-testid=dialog-input]").Focus());
        await test.EchoAsync(cut);

        Assert.Equal("Apple", cut.HighlightedText(store));
    }
}
