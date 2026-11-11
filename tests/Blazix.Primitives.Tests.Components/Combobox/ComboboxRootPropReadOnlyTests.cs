using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `prop: readOnly` titles cited at L2.
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>prop: readOnly</c> block.</summary>
public sealed class ComboboxRootPropReadOnlyTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should expose aria-autocomplete=\"none\" when readOnly")]
    [Trait("Requirement", "CMB-ARIA-04")]
    public void Should_expose_aria_autocomplete_none_when_readOnly()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["ReadOnly"] = true })
            .Add(x => x.WithPopup, false));

        Assert.Equal("none", cut.Find("[data-testid=input]").GetAttribute("aria-autocomplete"));
    }

    [Fact(DisplayName = "should set readOnly attribute on hidden input")]
    [Trait("Requirement", "CMB-DOM-18")]
    public void Should_set_readOnly_attribute_on_hidden_input()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["ReadOnly"] = true, ["Name"] = "test" })
            .Add(x => x.StaticItems, ["a"]));

        Assert.True(cut.Find("input[name=test]").HasAttribute("readonly"));
    }
}
