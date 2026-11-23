using AngleSharp.Dom;
using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `prop: disabled` titles cited at L2. A disabled trigger asks the `interactions` module for no
// press, so its click reaches C# as nothing; a disabled input receives no typing in the browser, and a typed change the
// module did report still does not open (ComboboxInput.tsx:249).
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>prop: disabled</c> block.</summary>
public sealed class ComboboxRootPropDisabledTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should render disabled state on all interactive components")]
    [Trait("Requirement", "CMB-API-30")]
    [Trait("Requirement", "CMB-STA-02")]
    [Trait("Requirement", "CMB-DOM-03")]
    [Trait("Requirement", "CMB-ARIA-17")]
    public void Should_render_disabled_state_on_all_interactive_components()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Disabled"] = true })
            .Add(x => x.WithTrigger, true)
            .Add(x => x.StaticItems, ["a", "b"]));
        IElement trigger = cut.Find("[data-testid=trigger]");

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("disabled"));
        Assert.True(trigger.HasAttribute("disabled"));

        // Verify interactions are disabled: the trigger's handle asks for no press, so a click opens nothing.
        string options = test.TriggerOptions(trigger.Id!);
        Assert.DoesNotContain("\"click\"", options, StringComparison.Ordinal);
        Assert.False(cut.HasList());
    }

    [Fact(DisplayName = "should prevent keyboard interactions when disabled")]
    [Trait("Requirement", "CMB-API-30")]
    public async Task Should_prevent_keyboard_interactions_when_disabled()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Disabled"] = true })
            .Add(x => x.StaticItems, ["a", "b"]));

        await cut.TypeAsync(test, "a");

        Assert.False(cut.Instance.Root!.Store.IsOpen);
        Assert.False(cut.HasList());
    }

    [Fact(DisplayName = "should set disabled attribute on hidden input")]
    [Trait("Requirement", "CMB-API-30")]
    [Trait("Requirement", "CMB-DOM-18")]
    public void Should_set_disabled_attribute_on_hidden_input()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Disabled"] = true, ["Name"] = "test" })
            .Add(x => x.StaticItems, ["a"]));

        Assert.True(cut.Find("input[name=test]").HasAttribute("disabled"));
    }
}
