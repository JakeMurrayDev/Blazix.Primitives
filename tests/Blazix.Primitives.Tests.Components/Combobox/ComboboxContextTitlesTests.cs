using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// Base UI's *Context.test.tsx titles (PR 193 delta review B1): a context hook outside its provider throws a descriptive error.
// The port has no public hooks; the parts that read each context throw it when rendered outside the provider. Also the
// Popup's open-state attribute (ComboboxPopup.test.tsx) and the Value's placeholder over a null item (ComboboxValue.test.tsx).
/// <summary>Combobox's context errors, the Popup's open state and the Value's placeholder at L2.</summary>
public sealed class ComboboxContextTitlesTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // ComboboxRootContext.test.tsx's cases are the root's contexts; the port's one root store stands for all three.
    [Theory(DisplayName = "$name throws when its provider is missing")]
    [InlineData("ComboboxRootContext")]
    [Trait("Requirement", "CMB-STR-02")]
    public void Name_throws_when_its_provider_is_missing(string name)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<ComboboxInput>());

        Assert.StartsWith($"Blazix: `{name}` is missing.", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "throws a descriptive error when used outside <Combobox.Item>")]
    [Trait("Requirement", "CMB-STR-02")]
    public void Throws_a_descriptive_error_when_used_outside_an_item()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RenderInRoot(builder =>
        {
            builder.OpenComponent<ComboboxItemIndicator>(0);
            builder.CloseComponent();
        }));

        Assert.StartsWith("Blazix: `ComboboxItemContext` is missing.", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "throws a descriptive error when used outside <Combobox.Group>")]
    [Trait("Requirement", "CMB-STR-02")]
    public void Throws_a_descriptive_error_when_used_outside_a_group()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RenderInRoot(builder =>
        {
            builder.OpenComponent<ComboboxGroupLabel>(0);
            builder.CloseComponent();
        }));

        Assert.StartsWith("Blazix: `ComboboxGroupContext` is missing.", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "throws a descriptive error when used outside <Combobox.Portal>")]
    [Trait("Requirement", "CMB-STR-04")]
    public void Throws_a_descriptive_error_when_used_outside_the_portal()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RenderInRoot(builder =>
        {
            builder.OpenComponent<ComboboxPositioner>(0);
            builder.CloseComponent();
        }));

        Assert.StartsWith("Blazix: `<ComboboxPortal>` is missing.", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "throws a descriptive error when a required consumer is outside the positioner")]
    [Trait("Requirement", "CMB-STR-04")]
    public void Throws_a_descriptive_error_when_a_required_consumer_is_outside_the_positioner()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RenderInRoot(builder =>
        {
            builder.OpenComponent<ComboboxPopup>(0);
            builder.CloseComponent();
        }));

        Assert.Equal("Blazix: `<ComboboxPopup>` and `<ComboboxArrow>` must be used within the `<ComboboxPositioner>` component.", error.Message);
    }

    [Fact(DisplayName = "exposes open state via data attributes mapping")]
    [Trait("Requirement", "CMB-STA-10")]
    [Trait("Requirement", "CMB-DOM-12")]
    public void Exposes_open_state_via_data_attributes_mapping()
    {
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Open"] = true, ["Items"] = (IReadOnlyList<string>)["apple"] }));

        Assert.True(cut.Find("[data-testid=popup]").HasAttribute("data-open"));
    }

    [Fact(DisplayName = "uses placeholder when items have null value without label")]
    [Trait("Requirement", "CMB-API-47")]
    public void Uses_placeholder_when_items_have_null_value_without_label()
    {
        IRenderedComponent<ComboboxTypedHarness<string?>> cut = test.Context.Render<ComboboxTypedHarness<string?>>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = (IReadOnlyList<string?>)[null, "option1"] })
            .Add(x => x.WithValue, true)
            .Add(x => x.ValuePlaceholder, "Select an option"));

        Assert.Contains("Select an option", cut.Find("[data-testid=value]").TextContent, StringComparison.Ordinal);
    }

    private IRenderedComponent<ComboboxRoot<string>> RenderInRoot(RenderFragment content) =>
        test.Context.Render<ComboboxRoot<string>>(p => p.Add(x => x.Open, true).Add(x => x.ChildContent, content));
}
