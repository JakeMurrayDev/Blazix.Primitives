namespace Blazix.Primitives.Tests;

// The [L1] rows of docs/components/toggle-group.md (issue 102): TGG-STA-03 and TGG-STA-04 over the pure value rules
// (toggle-group §4; toggle-group/ToggleGroup.tsx:61-71), titles verbatim from ToggleGroup.test.tsx. Each title replays the
// clicks of the Base UI test as calls of ToggleGroupValue.Next with the clicked toggle's identity and its next pressed
// state (TGL-PTR-01 step 1); the rendering half of each title is ToggleGroupTests (L2). The value-less toggles of "when
// Toggles omit value" are two generated identities. Port titles are marked as such.
/// <summary>The value rules of a toggle group: single-select replaces, multi-select appends or removes, and every call returns a new list.</summary>
public sealed class ToggleGroupValueTests
{
    private const string FirstGenerated = "base-ui-1";
    private const string SecondGenerated = "base-ui-2";

    [Fact(DisplayName = "only one item can be pressed when false")]
    [Trait("Requirement", "TGG-STA-03")]
    [Trait("Requirement", "TGG-API-07")]
    public void Only_one_item_can_be_pressed_when_false()
    {
        IReadOnlyList<string> value = ["one"];

        value = ToggleGroupValue.Next(value, "two", nextPressed: true, multiple: false);

        Assert.Equal(["two"], value);
    }

    [Fact(DisplayName = "pressed state")]
    [Trait("Requirement", "TGG-STA-03")]
    public void Pressed_state()
    {
        IReadOnlyList<string> value = [];

        value = ToggleGroupValue.Next(value, "one", nextPressed: true, multiple: false);
        Assert.Equal(["one"], value);

        value = ToggleGroupValue.Next(value, "two", nextPressed: true, multiple: false);
        Assert.Equal(["two"], value);
    }

    [Fact(DisplayName = "when Toggles omit value")]
    [Trait("Requirement", "TGG-STA-03")]
    public void When_toggles_omit_value_single()
    {
        IReadOnlyList<string> value = [];

        value = ToggleGroupValue.Next(value, FirstGenerated, nextPressed: true, multiple: false);
        Assert.Equal([FirstGenerated], value);

        value = ToggleGroupValue.Next(value, SecondGenerated, nextPressed: true, multiple: false);
        Assert.Equal([SecondGenerated], value);
    }

    [Fact(DisplayName = "port: pressing the pressed toggle again empties a single-select value")]
    [Trait("Requirement", "TGG-STA-03")]
    public void Port_pressing_the_pressed_toggle_again_empties_a_single_select_value()
    {
        IReadOnlyList<string> value = ["one"];

        value = ToggleGroupValue.Next(value, "one", nextPressed: false, multiple: false);

        Assert.Empty(value);
    }

    [Fact(DisplayName = "multiple items can be pressed when true")]
    [Trait("Requirement", "TGG-STA-04")]
    [Trait("Requirement", "TGG-API-07")]
    public void Multiple_items_can_be_pressed_when_true()
    {
        IReadOnlyList<string> value = ["one"];

        value = ToggleGroupValue.Next(value, "two", nextPressed: true, multiple: true);

        Assert.Equal(["one", "two"], value);
    }

    [Fact(DisplayName = "when Toggles omit value")]
    [Trait("Requirement", "TGG-STA-04")]
    public void When_toggles_omit_value_multiple()
    {
        IReadOnlyList<string> value = [];

        value = ToggleGroupValue.Next(value, FirstGenerated, nextPressed: true, multiple: true);
        Assert.Equal([FirstGenerated], value);

        value = ToggleGroupValue.Next(value, SecondGenerated, nextPressed: true, multiple: true);
        Assert.Equal([FirstGenerated, SecondGenerated], value);

        value = ToggleGroupValue.Next(value, FirstGenerated, nextPressed: false, multiple: true);
        Assert.Equal([SecondGenerated], value);
    }

    [Fact(DisplayName = "port: every call returns a new list and leaves the current one unchanged")]
    [Trait("Requirement", "TGG-STA-04")]
    [Trait("Requirement", "TGG-EVT-05")]
    public void Port_every_call_returns_a_new_list_and_leaves_the_current_one_unchanged()
    {
        string[] current = ["one", "two"];

        IReadOnlyList<string> appended = ToggleGroupValue.Next(current, "three", nextPressed: true, multiple: true);
        IReadOnlyList<string> removed = ToggleGroupValue.Next(current, "one", nextPressed: false, multiple: true);
        IReadOnlyList<string> replaced = ToggleGroupValue.Next(current, "two", nextPressed: true, multiple: false);
        IReadOnlyList<string> emptied = ToggleGroupValue.Next(["two"], "two", nextPressed: false, multiple: false);
        IReadOnlyList<string> emptiedAgain = ToggleGroupValue.Next(["two"], "two", nextPressed: false, multiple: false);

        Assert.Equal(["one", "two", "three"], appended);
        Assert.Equal(["two"], removed);
        Assert.Equal(["two"], replaced);
        Assert.Equal(["one", "two"], current);
        Assert.NotSame(current, appended);
        Assert.NotSame(current, removed);
        Assert.NotSame(current, replaced);
        Assert.NotSame(emptied, emptiedAgain);
    }

    [Fact(DisplayName = "port: membership is ordinal")]
    [Trait("Requirement", "TGG-STA-04")]
    public void Port_membership_is_ordinal()
    {
        IReadOnlyList<string> value = ["One", "one"];

        value = ToggleGroupValue.Next(value, "one", nextPressed: false, multiple: true);

        Assert.Equal(["One"], value);
    }

    [Fact(DisplayName = "port: a multi-select update keeps unknown identities, and a single-select one drops them")]
    [Trait("Requirement", "TGG-EDGE-05")]
    public void Port_a_multi_select_update_keeps_unknown_identities_and_a_single_select_one_drops_them()
    {
        IReadOnlyList<string> value = ["ghost", "one"];

        Assert.Equal(["ghost", "one", "two"], ToggleGroupValue.Next(value, "two", nextPressed: true, multiple: true));
        Assert.Equal(["ghost"], ToggleGroupValue.Next(value, "one", nextPressed: false, multiple: true));
        Assert.Equal(["two"], ToggleGroupValue.Next(value, "two", nextPressed: true, multiple: false));
    }

    [Fact(DisplayName = "port: unpressing a duplicated identity removes its first occurrence only")]
    [Trait("Requirement", "TGG-EDGE-04")]
    public void Port_unpressing_a_duplicated_identity_removes_its_first_occurrence_only()
    {
        IReadOnlyList<string> value = ["a", "b", "a"];

        Assert.Equal(["b", "a"], ToggleGroupValue.Next(value, "a", nextPressed: false, multiple: true));
        Assert.Empty(ToggleGroupValue.Next(["a"], "a", nextPressed: false, multiple: false));
    }
}
