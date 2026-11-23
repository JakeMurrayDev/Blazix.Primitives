using AngleSharp.Dom;
using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `prop: required` titles cited at L2. getByRole('textbox', { hidden: true }) is the hidden
// validation input, the one hidden input that is not of type `hidden`.
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>prop: required</c> block.</summary>
public sealed class ComboboxRootPropRequiredTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "does not mark the hidden input as required when selection exists in multiple mode")]
    [Trait("Requirement", "CMB-API-28")]
    public void Does_not_mark_the_hidden_input_as_required_when_selection_exists_in_multiple_mode()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render((IReadOnlyList<string>)["a"]);

        IElement hiddenInput = ValidationInput(cut);
        Assert.False(hiddenInput.HasAttribute("required"));
    }

    [Fact(DisplayName = "keeps the hidden input required when no selection exists in multiple mode")]
    [Trait("Requirement", "CMB-API-28")]
    [Trait("Requirement", "CMB-DOM-18")]
    public void Keeps_the_hidden_input_required_when_no_selection_exists_in_multiple_mode()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render([]);

        IElement hiddenInput = ValidationInput(cut);
        Assert.True(hiddenInput.HasAttribute("required"));
    }

    // The hidden inputs other than the Input itself; the validation input is the one whose type is not `hidden`.
    private static IElement ValidationInput(IRenderedComponent<ComboboxRootBatchBHarness> cut) =>
        Assert.Single(cut.FindAll("input:not([data-testid=input])"), input => !string.Equals(input.GetAttribute("type"), "hidden", StringComparison.Ordinal));

    private IRenderedComponent<ComboboxRootBatchBHarness> Render(IReadOnlyList<string> values) =>
        test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Multiple"] = true, ["Required"] = true, ["Name"] = "languages", ["Values"] = values })
            .Add(x => x.WithPopup, false));
}
