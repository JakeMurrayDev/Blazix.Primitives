using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `prop: id` titles cited at L2.
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>prop: id</c> block.</summary>
public sealed class ComboboxRootPropIdTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "sets the id on the input when it is outside the popup")]
    [Trait("Requirement", "CMB-API-27")]
    [Trait("Requirement", "CMB-API-39")]
    public void Sets_the_id_on_the_input_when_it_is_outside_the_popup()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Id"] = "test-id" })
            .Add(x => x.StaticItems, ["a", "b"]));

        Assert.Equal("test-id", cut.Find("[role=combobox]").Id);
    }
}
