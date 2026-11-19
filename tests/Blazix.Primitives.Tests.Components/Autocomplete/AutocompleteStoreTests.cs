using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// M6 track 11 (issue 257 (1)): combobox/store.test.tsx's two characterization titles, restated at L2 (ADR 13 §3). Base UI pins
// React mechanics the port does not have (a ref callback during the first commit; `store.subscribe` over a synchronization
// effect); the port's counterparts are a descendant's first initialization and the store's own subscription.
/// <summary>Autocomplete's store publication at L2.</summary>
public sealed class AutocompleteStoreTests : IDisposable
{
    private readonly ComboboxTestContext test = AutocompleteTesting.CreateContext();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // Restates "exposes real commands to a descendant ref callback on the first commit".
    [Fact(DisplayName = "port: a descendant's first initialization reaches the store's real open command")]
    [Trait("Requirement", "CMB-STA-34")]
    [Trait("Requirement", "CMB-STR-01")]
    [Trait("Coverage", "issue-257")]
    public void A_descendants_first_initialization_reaches_the_stores_real_open_command()
    {
        List<bool> opens = [];
        IRenderedComponent<AutocompleteStoreHost> cut = test.Context.Render<AutocompleteStoreHost>(p => p
            .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> args) => opens.Add(args.Value))
            .Add(x => x.Probe, builder =>
            {
                builder.OpenComponent<AutocompleteEarliestCommandProbe>(0);
                builder.CloseComponent();
            }));

        Assert.True(cut.FindComponent<AutocompleteEarliestCommandProbe>().Instance.Dispatched);
        Assert.Equal([true], opens);
        Assert.True(cut.Instance.Root!.Store.IsOpen);
    }

    // Restates "publishes an inline transition in a single store transaction".
    [Fact(DisplayName = "port: an inline transition publishes the input's form ownership in a single store write")]
    [Trait("Requirement", "CMB-STA-34")]
    [Trait("Requirement", "CMB-API-23")]
    [Trait("Coverage", "issue-257")]
    public void An_inline_transition_publishes_the_inputs_form_ownership_in_a_single_store_write()
    {
        IRenderedComponent<AutocompleteStoreHost> cut = test.Context.Render<AutocompleteStoreHost>();
        ComboboxStore<string> store = cut.Instance.Root!.Store;
        Assert.True(store.State.InputInsidePopup);
        Assert.Null(cut.Find("[data-testid=popup-input]").GetAttribute("name"));
        Assert.Single(cut.FindAll("[name=search]"));

        // The projection Base UI subscribes to: `inline` and whether the input owns the form value (AriaCombobox.tsx:1564).
        List<(bool Inline, bool InputOwnsFormValue)> snapshots = [];
        using IDisposable subscription = store.Subscribe(state => snapshots.Add((state.Inline, state.Inline || !state.InputInsidePopup)));

        cut.Render(p => p.Add(x => x.Inline, true));

        Assert.DoesNotContain(snapshots, snapshot => snapshot.Inline && !snapshot.InputOwnsFormValue);
        List<(bool Inline, bool InputOwnsFormValue)> changed = [.. snapshots.Where((snapshot, index) => index == 0 || snapshot != snapshots[index - 1])];
        Assert.Equal([(true, true)], changed);

        // The write's ownership is the markup's: the hidden input gives up the name. The Input inside the Positioner never takes it
        // (ComboboxInput.tsx:111,186), so no control is named here, as in Base UI (autocomplete.md §16 Q13).
        Assert.Empty(cut.FindAll("[name=search]"));
    }
}
