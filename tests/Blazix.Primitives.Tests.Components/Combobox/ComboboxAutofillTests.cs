using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-EVT-11 at L2 (ComboboxRoot.test.tsx's and createItems.test.tsx's autofill titles): the hidden input's autofilled
// `change`, which the `form` module reports (FormTwin.OnAutofill), selects the item whose serialized value or label matches
// the text case-insensitively, with the reason none; multiple mode, a disabled or read-only root, an unmatched text and a
// prevented change select nothing.
/// <summary>Combobox's browser autofill at L2.</summary>
public sealed class ComboboxAutofillTests : IDisposable
{
    private static readonly string[] s_fruits = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should handle browser autofill")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Should_handle_browser_autofill()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits });

        await AutofillAsync(cut, "banana");

        (string? value, EventReason reason) = Assert.Single(cut.Instance.ValueChanges);
        Assert.Equal("banana", value);
        Assert.Equal(EventReason.None, reason);
        Assert.Empty(cut.Instance.OpenChanges);
    }

    [Fact(DisplayName = "matches browser autofill against an item rendered label for primitive values regardless of case")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Matches_regardless_of_case()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits });

        await AutofillAsync(cut, "CHERRY");

        Assert.Equal("cherry", Assert.Single(cut.Instance.ValueChanges).Value);
    }

    // ComboboxRoot.test.tsx: object items with itemToStringLabel and itemToStringValue; an autofill with the value's form text
    // selects the item. The port's collection derives the value (the code) and the label (the country).
    [Fact(DisplayName = "should handle browser autofill with object values")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Should_handle_browser_autofill_with_object_values()
    {
        IReadOnlyList<string> items = ComboboxItems.Create<(string Code, string Country), string>([("US", "United States"), ("CA", "Canada")], item => item.Code, item => item.Country);
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = items });

        await AutofillAsync(cut, "CA");

        Assert.Equal("CA", Assert.Single(cut.Instance.ValueChanges).Value);
    }

    [Fact(DisplayName = "should handle browser autofill with object values when autofill uses the label")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Matches_the_label_of_a_collection_item()
    {
        IReadOnlyList<string> items = ComboboxItems.Create<(string Id, string Name), string>([("a", "Apple"), ("b", "Banana")], item => item.Id, item => item.Name);
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = items });

        await AutofillAsync(cut, "banana");

        Assert.Equal("b", Assert.Single(cut.Instance.ValueChanges).Value);
    }

    [Fact(DisplayName = "ignores scalar browser autofill in multiple mode")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Ignores_autofill_in_multiple_mode()
    {
        IRenderedComponent<ComboboxHarness> single = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits });
        await AutofillAsync(single, "apple");
        Assert.Single(single.Instance.ValueChanges);

        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits, ["Multiple"] = true });
        await AutofillAsync(cut, "apple");

        Assert.Empty(cut.Instance.ValuesChanges);
    }

    [Fact(DisplayName = "ignores unmatched browser autofill in single mode")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Ignores_unmatched_autofill()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits });
        await AutofillAsync(cut, "apple");
        Assert.Single(cut.Instance.ValueChanges);

        await AutofillAsync(cut, "durian");

        Assert.Single(cut.Instance.ValueChanges);
        Assert.Equal("apple", cut.Instance.Root!.Store.SelectedValue);
    }

    [Theory(DisplayName = "ignores hidden-input autofill when $lockState $label")]
    [Trait("Requirement", "CMB-EVT-11")]
    [InlineData("Disabled")]
    [InlineData("ReadOnly")]
    public async Task Ignores_autofill_when_locked(string lockState)
    {
        IRenderedComponent<ComboboxHarness> open = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits });
        await AutofillAsync(open, "apple");
        Assert.Single(open.Instance.ValueChanges);

        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits, [lockState] = true });
        await AutofillAsync(cut, "apple");

        Assert.Empty(cut.Instance.ValueChanges);
    }

    [Fact(DisplayName = "port: a prevented autofill change selects nothing")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task A_prevented_autofill_change_selects_nothing()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits });
        await AutofillAsync(cut, "apple");
        Assert.Single(cut.Instance.ValueChanges);

        await AutofillAsync(cut, "banana", prevented: true);

        Assert.Single(cut.Instance.ValueChanges);
    }

    private Task AutofillAsync(IRenderedComponent<ComboboxHarness> cut, string text, bool prevented = false)
    {
        JSRuntimeInvocation attach = test.Form.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains("\"reportAutofill\":true", StringComparison.Ordinal));
        FormTwin twin = ((DotNetObjectReference<FormTwin>)attach.Arguments[2]!).Value;
        string payload = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"defaultPrevented":{{(prevented ? "true" : "false")}},"event":{{ComboboxTestContext.InputEvent}}}""";
        return cut.InvokeAsync(() => twin.OnAutofill(payload));
    }

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root) =>
        test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, root));
}
