using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `initial input value derivation` titles cited at L2 that ComboboxRootTests does not hold. The
// input's text is the store's (the module writes the DOM value). Base UI's `{ value, label }` items label through their
// `label` field; the port reads no member of an item (ADR 16 rule 1, shared-utilities' ValueLabels), so the consumer's
// ItemToStringLabel names it.
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>initial input value derivation</c> block.</summary>
public sealed class ComboboxRootInitialInputValueDerivationTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "derives input from defaultValue on first mount with items prop")]
    [Trait("Requirement", "CMB-API-12")]
    [Trait("Requirement", "CMB-STA-19")]
    public void Derives_input_from_defaultValue_on_first_mount_with_items_prop()
    {
        Fruit[] items = [new("apple", "Apple")];

        IRenderedComponent<ComboboxTypedHarness<Fruit>> cut = test.Context.Render<ComboboxTypedHarness<Fruit>>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Items"] = (IReadOnlyList<Fruit>)items,
                ["DefaultValue"] = items[0],
                ["ItemToStringLabel"] = (Func<Fruit, string>)(item => item.Label),
            })
            .Add(x => x.WithList, false));

        Assert.Equal("Apple", cut.InputText());
    }

    [Fact(DisplayName = "derives input from controlled value on first mount when unspecified")]
    [Trait("Requirement", "CMB-STA-19")]
    public void Derives_input_from_controlled_value_on_first_mount_when_unspecified()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["Value"] = "banana" });

        Assert.Equal("banana", cut.InputText());
    }

    [Fact(DisplayName = "inputValue overrides derivation when provided")]
    [Trait("Requirement", "CMB-API-09")]
    public void InputValue_overrides_derivation_when_provided()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["Value"] = "apple", ["InputValue"] = "x" });

        Assert.Equal("x", cut.InputText());
    }

    private IRenderedComponent<ComboboxRootBatchBHarness> Render(Dictionary<string, object?> parameters) =>
        test.Context.Render<ComboboxRootBatchBHarness>(p => p.Add(x => x.RootParameters, parameters).Add(x => x.WithPopup, false));

    private sealed record Fruit(string Value, string Label);
}
