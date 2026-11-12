using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `input value synchronization` titles cited at L2. A parameter set re-derives the input text, so
// these titles pass empty callbacks for the ones they do not observe (an EventCallback renders its receiver, which sets
// the Root's parameters again) and record the observed ones on the test class, which renders nothing. setProps is a
// render of the harness with the new parameters. Base UI's `{ value, label }` items label through their `label` field;
// the port reads no member of an item (ADR 16 rule 1), so the consumer's ItemToStringLabel names it.
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>input value synchronization</c> block.</summary>
public sealed class ComboboxRootInputValueSynchronizationTests : IDisposable
{
    private static readonly Func<Fruit, string> s_label = item => item.Label;

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "updates derived input when controlled value changes externally")]
    [Trait("Requirement", "CMB-STA-20")]
    public void Updates_derived_input_when_controlled_value_changes_externally()
    {
        Fruit[] items = [new("apple", "Apple"), new("banana", "Banana")];
        IRenderedComponent<ComboboxTypedHarness<Fruit>> cut = RenderFruits(Fruits(items, items[0]));
        Assert.Equal("Apple", cut.InputText());

        cut.Render(p => p.Add(x => x.RootParameters, Fruits(items, items[1])));

        Assert.Equal("Banana", cut.InputText());
    }

    // React's StrictMode (`strict: true`) runs renders and effects twice in development; Blazor has no counterpart, so the
    // title's two cases are one.
    [Fact(DisplayName = "does not force-mount the list when controlled value changes externally (strict: %s)")]
    [Trait("Requirement", "CMB-STA-32")]
    [Trait("Requirement", "CMB-EDGE-07")]
    public void Does_not_force_mount_the_list_when_controlled_value_changes_externally()
    {
        Dictionary<string, string> labels = new() { ["apple"] = "Apple", ["banana"] = "Banana" };
        Func<string, string> label = item => labels[item];
        Dictionary<string, object?> WithItems(string? value) => Quiet(new()
        {
            ["Items"] = (IReadOnlyList<string>)["apple", "banana"],
            ["Value"] = value,
            ["ItemToStringLabel"] = label,
        });
        Dictionary<string, object?> WithoutItems(string? value) => Quiet(new() { ["Value"] = value, ["ItemToStringLabel"] = label });

        IRenderedComponent<ComboboxRootBatchBHarness> itemsRoot = test.Context.Render<ComboboxRootBatchBHarness>(p => p.Add(x => x.RootParameters, WithItems(null)));
        IRenderedComponent<ComboboxRootBatchBHarness> plainRoot = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, WithoutItems(null))
            .Add(x => x.StaticItems, ["apple", "banana"]));
        Assert.Empty(itemsRoot.FindAll("[role=listbox]"));
        Assert.Empty(plainRoot.FindAll("[role=listbox]"));

        // The Set button: both values become "banana" in one consumer render.
        itemsRoot.Render(p => p.Add(x => x.RootParameters, WithItems("banana")));
        plainRoot.Render(p => p.Add(x => x.RootParameters, WithoutItems("banana")));

        Assert.Empty(itemsRoot.FindAll("[role=listbox]"));
        Assert.Empty(plainRoot.FindAll("[role=listbox]"));
        Assert.Equal("Banana", itemsRoot.InputText());
        Assert.Equal("Banana", plainRoot.InputText());
    }

    [Fact(DisplayName = "re-derives input when items array changes")]
    [Trait("Requirement", "CMB-STA-20")]
    public void Re_derives_input_when_items_array_changes()
    {
        Fruit[] initialItems = [new("a", "Apple"), new("b", "Banana")];
        IRenderedComponent<ComboboxTypedHarness<Fruit>> cut = RenderFruits(Fruits(initialItems, initialItems[0]));
        Assert.Equal("Apple", cut.InputText());

        Fruit[] nextItems = [new("a", "Apricot"), new("b", "Banana"), new("c", "Cherry")];
        cut.Render(p => p.Add(x => x.RootParameters, Fruits(nextItems, nextItems[0])));
        Assert.Equal("Apricot", cut.InputText());

        Fruit[] sameLengthDifferentItems = [new("a", "Ambrosia"), new("b", "Blue Java"), new("c", "Clementine")];
        cut.Render(p => p.Add(x => x.RootParameters, Fruits(sameLengthDifferentItems, sameLengthDifferentItems[0])));
        Assert.Equal("Ambrosia", cut.InputText());
    }

    [Fact(DisplayName = "restores the selected label after a one-step clear and items reload")]
    [Trait("Requirement", "CMB-STA-20")]
    public async Task Restores_the_selected_label_after_a_one_step_clear_and_items_reload()
    {
        Func<string, string> itemToStringLabel = item => item == "apple" ? "Apple" : item;
        Dictionary<string, object?> Parameters() => Quiet(new()
        {
            ["Items"] = (IReadOnlyList<string>)["apple"],
            ["Value"] = "apple",
            ["ItemToStringLabel"] = itemToStringLabel,
        });
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p.Add(x => x.RootParameters, Parameters()).Add(x => x.WithPopup, false));
        Assert.Equal("Apple", cut.InputText());

        await cut.TypeAsync(test, string.Empty);
        Assert.Equal(string.Empty, cut.InputText());

        cut.Render(p => p.Add(x => x.RootParameters, Parameters()));

        Assert.Equal("Apple", cut.InputText());
    }

    [Fact(DisplayName = "reports a single input value change when the controlled value changes")]
    [Trait("Requirement", "CMB-API-11")]
    [Trait("Requirement", "CMB-STA-20")]
    [Trait("Requirement", "CMB-EVT-04")]
    public void Reports_a_single_input_value_change_when_the_controlled_value_changes()
    {
        int calls = 0;
        Fruit[] items = [new("a", "Apple"), new("b", "Banana"), new("c", "Cherry")];
        Dictionary<string, object?> Parameters(Fruit value)
        {
            Dictionary<string, object?> parameters = Fruits(items, value);
            parameters["OnInputValueChange"] = EventCallback.Factory.Create<ValueChangeEventArgs<string>>(this, _ => calls++);
            return parameters;
        }

        IRenderedComponent<ComboboxTypedHarness<Fruit>> cut = RenderFruits(Parameters(items[0]));

        cut.Render(p => p.Add(x => x.RootParameters, Parameters(items[1])));
        Assert.Equal("Banana", cut.InputText());
        Assert.Equal(1, calls);

        cut.Render(p => p.Add(x => x.RootParameters, Parameters(items[2])));
        Assert.Equal("Cherry", cut.InputText());
        Assert.Equal(2, calls);

        cut.Render(p => p.Add(x => x.RootParameters, Parameters(items[0])));
        Assert.Equal("Apple", cut.InputText());
        Assert.Equal(3, calls);
    }

    [Fact(DisplayName = "syncs again after a canceled sync to the same label")]
    [Trait("Requirement", "CMB-API-11")]
    [Trait("Requirement", "CMB-STA-20")]
    public void Syncs_again_after_a_canceled_sync_to_the_same_label()
    {
        bool cancelSync = false;
        EventCallback<ValueChangeEventArgs<string>> onInputValueChange = EventCallback.Factory.Create<ValueChangeEventArgs<string>>(this, args =>
        {
            if (cancelSync && args.Details.Reason == EventReason.None)
            {
                args.Details.Cancel();
            }
        });
        Dictionary<string, object?> Parameters(string value) => Quiet(new()
        {
            ["Items"] = (IReadOnlyList<string>)["One", "Two"],
            ["Value"] = value,
            ["OnInputValueChange"] = onInputValueChange,
        });

        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p.Add(x => x.RootParameters, Parameters("One")).Add(x => x.WithPopup, false));
        Assert.Equal("One", cut.InputText());

        cancelSync = true;
        cut.Render(p => p.Add(x => x.RootParameters, Parameters("Two")));
        Assert.Equal("One", cut.InputText());

        // The input already matches this label, so nothing is written and nothing observes a change.
        cancelSync = false;
        cut.Render(p => p.Add(x => x.RootParameters, Parameters("One")));
        Assert.Equal("One", cut.InputText());

        cut.Render(p => p.Add(x => x.RootParameters, Parameters("Two")));
        Assert.Equal("Two", cut.InputText());
    }

    // The App's setValue is the harness's own render after OnValueChange (the callback's receiver), with the remapped value.
    [Fact(DisplayName = "reports a single input value change when the selection is remapped")]
    [Trait("Requirement", "CMB-STA-20")]
    public async Task Reports_a_single_input_value_change_when_the_selection_is_remapped()
    {
        Dictionary<string, object?> parameters = new()
        {
            ["Items"] = (IReadOnlyList<string>)["Apple", "Banana", "Cherry"],
            ["Value"] = "Apple",
            ["DefaultOpen"] = true,
            ["OnOpenChange"] = default(EventCallback<ValueChangeEventArgs<bool>>),
            ["OnItemHighlighted"] = default(EventCallback<ComboboxItemHighlightedEventArgs<string>>),
        };
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, parameters)
            .Add(x => x.Inline, true));
        cut.Instance.OnValue = args => parameters["Value"] = args.Value == "Banana" ? "Cherry" : args.Value ?? string.Empty;

        await cut.ClickOptionAsync("Banana");

        Assert.Equal("Cherry", cut.InputText());
        (string, EventReason)[] expected = [("Banana", EventReason.ItemPress), ("Cherry", EventReason.None)];
        Assert.Equal(expected, cut.Instance.InputChanges.Select(change => (change.Text, change.Reason)));
    }

    [Fact(DisplayName = "re-syncs the input when the resolved label of an unchanged value changes")]
    [Trait("Requirement", "CMB-STA-20")]
    public void Re_syncs_the_input_when_the_resolved_label_of_an_unchanged_value_changes()
    {
        Dictionary<string, object?> Parameters(Func<string, string> itemToStringLabel) => Quiet(new()
        {
            ["Items"] = (IReadOnlyList<string>)["a", "b"],
            ["Value"] = "b",
            ["ItemToStringLabel"] = itemToStringLabel,
        });
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p.Add(x => x.RootParameters, Parameters(value => value)).Add(x => x.WithPopup, false));
        Assert.Equal("b", cut.InputText());

        cut.Render(p => p.Add(x => x.RootParameters, Parameters(value => value == "b" ? "Banana" : value)));

        Assert.Equal("Banana", cut.InputText());
    }

    [Fact(DisplayName = "restores derived input after items load asynchronously")]
    [Trait("Requirement", "CMB-STA-20")]
    [Trait("Requirement", "CMB-EDGE-04")]
    public void Restores_derived_input_after_items_load_asynchronously()
    {
        Dictionary<string, object?> Parameters(IReadOnlyList<string> items) => Quiet(new() { ["Items"] = items, ["Value"] = "banana" });
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p.Add(x => x.RootParameters, Parameters([])).Add(x => x.WithPopup, false));
        Assert.Equal("banana", cut.InputText());

        cut.Render(p => p.Add(x => x.RootParameters, Parameters(["apple", "banana", "bread"])));
        Assert.Equal("banana", cut.InputText());

        cut.Render(p => p.Add(x => x.RootParameters, Parameters(["banana"])));
        Assert.Equal("banana", cut.InputText());
    }

    // The harness's recorders, empty: none renders the harness, so only a test's own render sets the Root's parameters.
    private static Dictionary<string, object?> Quiet(Dictionary<string, object?> parameters)
    {
        parameters.TryAdd("OnInputValueChange", default(EventCallback<ValueChangeEventArgs<string>>));
        parameters.TryAdd("OnOpenChange", default(EventCallback<ValueChangeEventArgs<bool>>));
        parameters.TryAdd("OnValueChange", default(EventCallback<ValueChangeEventArgs<string>>));
        parameters.TryAdd("OnItemHighlighted", default(EventCallback<ComboboxItemHighlightedEventArgs<string>>));
        return parameters;
    }

    private static Dictionary<string, object?> Fruits(Fruit[] items, Fruit value) => new()
    {
        ["Items"] = (IReadOnlyList<Fruit>)items,
        ["Value"] = value,
        ["ItemToStringLabel"] = s_label,
        ["OnInputValueChange"] = default(EventCallback<ValueChangeEventArgs<string>>),
        ["OnValueChange"] = default(EventCallback<ValueChangeEventArgs<Fruit>>),
        ["OnItemHighlighted"] = default(EventCallback<ComboboxItemHighlightedEventArgs<Fruit>>),
    };

    private IRenderedComponent<ComboboxTypedHarness<Fruit>> RenderFruits(Dictionary<string, object?> parameters) =>
        test.Context.Render<ComboboxTypedHarness<Fruit>>(p => p.Add(x => x.RootParameters, parameters).Add(x => x.WithList, false));

    private sealed record Fruit(string Value, string Label);
}
