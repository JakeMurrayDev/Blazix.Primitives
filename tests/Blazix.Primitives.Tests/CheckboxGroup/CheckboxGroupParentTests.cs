namespace Blazix.Primitives.Tests;

// The [L1] rows of docs/components/checkbox-group.md (issue 82): CKG-STA-03, CKG-STA-07, CKG-STA-08 and CKG-EDGE-04 over the
// pure parent algorithm (checkbox-group §4; useCheckboxGroupParent.ts), titles verbatim from CheckboxGroup.test.tsx and
// useCheckboxGroupParent.test.tsx. Each title replays the clicks of the Base UI test as proposals and commits: a member
// click is ProposeMemberToggle then, unless canceled, CommitMemberToggle; a parent click is ProposeParentToggle then, unless
// canceled, CommitParentToggle. The rendering half of each title is CheckboxGroupTests (L2).
/// <summary>The parent algorithm of a checkbox group: member proposals, the parent's cycle, the snapshot and the registries.</summary>
public sealed class CheckboxGroupParentTests
{
    private static readonly string[] s_all = ["a", "b", "c"];

    [Fact(DisplayName = "should be called when the value changes")]
    [Trait("Requirement", "CKG-STA-03")]
    public void Should_be_called_when_the_value_changes()
    {
        IReadOnlyList<string> value = [];

        value = CheckboxGroupParent.ProposeMemberToggle(value, "red", check: true);
        Assert.Equal(["red"], value);
        value = CheckboxGroupParent.ProposeMemberToggle(value, "green", check: true);
        Assert.Equal(["red", "green"], value);
        IReadOnlyList<string> previous = value;
        value = CheckboxGroupParent.ProposeMemberToggle(value, "blue", check: true);
        Assert.Equal(["red", "green", "blue"], value);

        // A commit produces a new list.
        Assert.NotSame(previous, value);
    }

    [Fact(DisplayName = "should treat an omitted defaultValue as an empty array")]
    [Trait("Requirement", "CKG-STA-03")]
    public void Should_treat_an_omitted_defaultValue_as_an_empty_array()
    {
        IReadOnlyList<string> value = [];

        value = CheckboxGroupParent.ProposeMemberToggle(value, "red", check: true);
        value = CheckboxGroupParent.ProposeMemberToggle(value, "green", check: true);
        value = CheckboxGroupParent.ProposeMemberToggle(value, "red", check: false);

        Assert.Equal(["green"], value);
    }

    [Fact(DisplayName = "prop: validationMode=onSubmit")]
    [Trait("Requirement", "CKG-STA-03")]
    public void Prop_validationMode_onSubmit()
    {
        IReadOnlyList<string> value = [];

        value = CheckboxGroupParent.ProposeMemberToggle(value, "two", check: true);
        value = CheckboxGroupParent.ProposeMemberToggle(value, "one", check: true);
        Assert.Equal(["two", "one"], value);
        value = CheckboxGroupParent.ProposeMemberToggle(value, "two", check: false);
        value = CheckboxGroupParent.ProposeMemberToggle(value, "three", check: true);
        Assert.Equal(["one", "three"], value);
    }

    [Fact(DisplayName = "port: removing an identity that is absent from the value changes nothing")]
    [Trait("Requirement", "CKG-STA-03")]
    public void Port_removing_an_identity_that_is_absent_changes_nothing()
    {
        // checkbox-group §16 Q5: Base UI's splice(indexOf(v), 1) would remove the last entry; the port guards it.
        Assert.Equal(["a", "b"], CheckboxGroupParent.ProposeMemberToggle(["a", "b"], "z", check: false));
    }

    [Fact(DisplayName = "should control child checkboxes")]
    [Trait("Requirement", "CKG-STA-07")]
    public void Should_control_child_checkboxes()
    {
        CheckboxGroupParent parent = new([]);
        IReadOnlyList<string> value = [];

        value = ClickParent(parent, value);
        Assert.Equal(s_all, value);
        Assert.True(CheckboxGroupParent.IsChecked(value, s_all));

        value = ClickParent(parent, value);
        Assert.Empty(value);
        Assert.False(CheckboxGroupParent.IsChecked(value, s_all));
        Assert.False(CheckboxGroupParent.IsIndeterminate(value, s_all));
    }

    [Fact(DisplayName = "updates uncontrolled parent-enabled groups from child clicks without duplicate callbacks")]
    [Trait("Requirement", "CKG-STA-07")]
    [Trait("Requirement", "CKG-STA-03")]
    public void Updates_uncontrolled_parent_enabled_groups_from_child_clicks_without_duplicate_callbacks()
    {
        CheckboxGroupParent parent = new([]);
        IReadOnlyList<string> value = [];

        value = ClickMember(parent, value, "a", check: true);
        Assert.Equal(["a"], value);
        Assert.True(CheckboxGroupParent.IsIndeterminate(value, s_all));

        value = ClickParent(parent, value);
        Assert.Equal(["a", "b", "c"], value);

        value = ClickParent(parent, value);
        Assert.Empty(value);
    }

    [Fact(DisplayName = "preserves initial state if mixed when parent is clicked")]
    [Trait("Requirement", "CKG-STA-07")]
    [Trait("Requirement", "CKG-STA-08")]
    public void Preserves_initial_state_if_mixed_when_parent_is_clicked()
    {
        CheckboxGroupParent parent = new([]);
        IReadOnlyList<string> value = [];

        value = ClickMember(parent, value, "a", check: true);
        Assert.True(CheckboxGroupParent.IsIndeterminate(value, s_all));

        value = ClickParent(parent, value);
        Assert.Equal(s_all, value);
        value = ClickParent(parent, value);
        Assert.Empty(value);
        value = ClickParent(parent, value);

        Assert.Equal(["a"], value);
        Assert.True(CheckboxGroupParent.IsIndeterminate(value, s_all));
    }

    [Fact(DisplayName = "handles unchecked disabled checkboxes")]
    [Trait("Requirement", "CKG-STA-07")]
    public void Handles_unchecked_disabled_checkboxes()
    {
        CheckboxGroupParent parent = new([]);
        parent.SetDisabled("a", true);
        IReadOnlyList<string> value = [];

        value = ClickParent(parent, value);

        Assert.Equal(["b", "c"], value);
        Assert.True(CheckboxGroupParent.IsIndeterminate(value, s_all));
    }

    [Fact(DisplayName = "handles checked disabled checkboxes")]
    [Trait("Requirement", "CKG-STA-07")]
    public void Handles_checked_disabled_checkboxes()
    {
        CheckboxGroupParent parent = new(["a"]);
        parent.SetDisabled("a", true);
        IReadOnlyList<string> value = ["a"];

        value = ClickParent(parent, value);
        Assert.Equal(["a", "b", "c"], value);

        value = ClickParent(parent, value);
        Assert.Equal(["a"], value);
    }

    [Fact(DisplayName = "does not advance the parent toggle cycle when the group cancels a parent change")]
    [Trait("Requirement", "CKG-STA-07")]
    [Trait("Requirement", "CKG-STA-08")]
    public void Does_not_advance_the_parent_toggle_cycle_when_the_group_cancels_a_parent_change()
    {
        CheckboxGroupParent parent = new(["a"]);
        IReadOnlyList<string> value = ["a"];

        // Canceled twice: the controlled value stays ["a"] and the cycle stays at "mixed → on".
        ParentProposal first = parent.ProposeParentToggle(value, s_all);
        ParentProposal second = parent.ProposeParentToggle(value, s_all);

        Assert.Equal(s_all, first.Value);
        Assert.Equal(s_all, second.Value);
    }

    [Fact(DisplayName = "does not pollute the parent snapshot when the group cancels a child change")]
    [Trait("Requirement", "CKG-STA-08")]
    public void Does_not_pollute_the_parent_snapshot_when_the_group_cancels_a_child_change()
    {
        CheckboxGroupParent parent = new(s_all);
        IReadOnlyList<string> value = s_all;

        // Unchecking a is proposed and canceled: no commit, so the snapshot stays all three.
        IReadOnlyList<string> proposed = CheckboxGroupParent.ProposeMemberToggle(value, "a", check: false);
        Assert.Equal(["b", "c"], proposed);

        Assert.Empty(parent.ProposeParentToggle(value, s_all).Value);
    }

    [Fact(DisplayName = "port: a controlled value the consumer changes does not re-sync the snapshot")]
    [Trait("Requirement", "CKG-STA-08")]
    public void Port_a_controlled_value_the_consumer_changes_does_not_resync_the_snapshot()
    {
        // checkbox-group §16 Q6, mirrored: the snapshot is the first-render value until a member toggle commits.
        CheckboxGroupParent parent = new(["a"]);
        IReadOnlyList<string> value = ["a", "b"];

        value = ClickParent(parent, value);
        Assert.Equal(s_all, value);
        value = ClickParent(parent, value);
        Assert.Empty(value);
        value = ClickParent(parent, value);

        Assert.Equal(["a"], value);
    }

    [Fact(DisplayName = "port: the parent derives from lengths, so entries outside allValues count")]
    [Trait("Requirement", "CKG-EDGE-10")]
    [Trait("Requirement", "CKG-STA-06")]
    public void Port_the_parent_derives_from_lengths()
    {
        Assert.True(CheckboxGroupParent.IsChecked(["cherry"], ["apple"]));
        Assert.True(CheckboxGroupParent.IsChecked([], []));
        Assert.True(CheckboxGroupParent.IsIndeterminate(["a", "b", "c", "d"], s_all));
        Assert.False(CheckboxGroupParent.IsIndeterminate([], s_all));
    }

    [Fact(DisplayName = "port: a changed allValues re-derives the parent and its next proposal without a reset")]
    [Trait("Requirement", "CKG-EDGE-11")]
    public void Port_a_changed_allValues_re_derives_without_a_reset()
    {
        CheckboxGroupParent parent = new([]);
        IReadOnlyList<string> value = ClickMember(parent, [], "a", check: true);

        Assert.Equal(["a", "b"], parent.ProposeParentToggle(value, ["a", "b"]).Value);
        Assert.True(CheckboxGroupParent.IsChecked(value, ["a"]));
    }

    [Fact(DisplayName = "does not read aria-controls ids off Object.prototype")]
    [Trait("Requirement", "CKG-EDGE-04")]
    [Trait("Requirement", "CKG-ARIA-06")]
    public void Does_not_read_aria_controls_ids_off_Object_prototype()
    {
        CheckboxGroupParent parent = new([]);
        parent.RegisterChild("a", "id-a");

        Assert.Equal("id-a", parent.AriaControls(["a", "constructor", "__proto__", "toString"]));

        parent.SetDisabled("constructor", true);
        Assert.Equal(["a", "__proto__", "toString"], parent.ProposeParentToggle([], ["a", "constructor", "__proto__", "toString"]).Value);
    }

    [Fact(DisplayName = "port: aria-controls follows allValues, then mount order, and is absent while empty")]
    [Trait("Requirement", "CKG-ARIA-06")]
    public void Port_aria_controls_follows_allValues_then_mount_order()
    {
        CheckboxGroupParent parent = new([]);
        Assert.Null(parent.AriaControls(s_all));

        parent.RegisterChild("c", "id-c");
        parent.RegisterChild("b", "id-b1");
        parent.RegisterChild("a", "id-a");
        parent.RegisterChild("b", "id-b2");
        parent.RegisterChild("z", "id-z");
        Assert.Equal("id-a id-b1 id-b2 id-c", parent.AriaControls(s_all));

        parent.UnregisterChild("b", "id-b1");
        Assert.Equal("id-a id-b2 id-c", parent.AriaControls(s_all));
    }

    private static IReadOnlyList<string> ClickParent(CheckboxGroupParent parent, IReadOnlyList<string> value)
    {
        ParentProposal proposal = parent.ProposeParentToggle(value, s_all);
        parent.CommitParentToggle(proposal);
        return proposal.Value;
    }

    private static IReadOnlyList<string> ClickMember(CheckboxGroupParent parent, IReadOnlyList<string> value, string identity, bool check)
    {
        IReadOnlyList<string> next = CheckboxGroupParent.ProposeMemberToggle(value, identity, check);
        parent.CommitMemberToggle(next);
        return next;
    }
}
