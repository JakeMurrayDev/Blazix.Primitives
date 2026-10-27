namespace Blazix.Primitives.Tests.Accordion;

/// <summary>
/// The <c>[L1]</c> rows ACC-STA-04/05/06 on the value algorithm as pure C# (Base UI's <c>handleValueChange</c>,
/// <c>accordion/root/AccordionRoot.tsx:67-89</c>, and the membership test <c>openValues.includes(value)</c>,
/// <c>accordion/item/AccordionItem.tsx:54</c>), with the edge rows ACC-EDGE-09..12 as port titles. The titles are Base UI's;
/// where Base UI repeats a title under several <c>describe</c> blocks, the block is named in parentheses.
/// </summary>
public sealed class AccordionValuesTests
{
    [Fact(DisplayName = "custom item value (uncontrolled)")]
    [Trait("Requirement", "ACC-STA-04")]
    public void Custom_item_value_uncontrolled()
    {
        // <Accordion.Root defaultValue={['two']}>: the item whose value is 'two' is open, 'one' is closed.
        IReadOnlyList<string> value = ["two"];

        Assert.False(AccordionValues.Contains(value, "one"));
        Assert.True(AccordionValues.Contains(value, "two"));
    }

    [Fact(DisplayName = "custom item value (controlled)")]
    [Trait("Requirement", "ACC-STA-04")]
    public void Custom_item_value_controlled()
    {
        // value={['two']}, then setProps({ value: ['one'] }).
        Assert.True(AccordionValues.Contains<string>(["two"], "two"));
        Assert.False(AccordionValues.Contains<string>(["one"], "two"));
        Assert.True(AccordionValues.Contains<string>(["one"], "one"));
    }

    [Fact(DisplayName = "when false only one item can be open")]
    [Trait("Requirement", "ACC-STA-05")]
    public void When_false_only_one_item_can_be_open()
    {
        // Two items without a value: their identities are generated strings.
        IReadOnlyList<string> afterFirst = AccordionValues.Next(AccordionValues.Empty<string>(), "base-ui-1", nextOpen: true, multiple: false);
        IReadOnlyList<string> afterSecond = AccordionValues.Next(afterFirst, "base-ui-4", nextOpen: true, multiple: false);

        Assert.Equal(["base-ui-1"], afterFirst);
        Assert.Equal(["base-ui-4"], afterSecond);
        Assert.False(AccordionValues.Contains(afterSecond, "base-ui-1"));
    }

    [Fact(DisplayName = "`multiple` is false")]
    [Trait("Requirement", "ACC-STA-05")]
    public void Multiple_is_false()
    {
        IReadOnlyList<string> first = AccordionValues.Next(AccordionValues.Empty<string>(), "one", nextOpen: true, multiple: false);
        IReadOnlyList<string> second = AccordionValues.Next(first, "two", nextOpen: true, multiple: false);

        Assert.Equal(["one"], first);
        Assert.Equal(["two"], second);
        // Pressing the open item closes it, whatever direction is requested (the direction is not consulted).
        Assert.Empty(AccordionValues.Next(second, "two", nextOpen: true, multiple: false));
    }

    [Fact(DisplayName = "default item value")]
    [Trait("Requirement", "ACC-STA-06")]
    public void Default_item_value()
    {
        IReadOnlyList<int> first = AccordionValues.Next(AccordionValues.Empty<int>(), 0, nextOpen: true, multiple: true);
        IReadOnlyList<int> second = AccordionValues.Next(first, 1, nextOpen: true, multiple: true);

        Assert.Equal([0], first);
        Assert.Equal([0, 1], second);
    }

    [Fact(DisplayName = "custom item value (onValueChange)")]
    [Trait("Requirement", "ACC-STA-06")]
    public void Custom_item_value_on_value_change()
    {
        IReadOnlyList<string> first = AccordionValues.Next(AccordionValues.Empty<string>(), "two", nextOpen: true, multiple: true);
        IReadOnlyList<string> second = AccordionValues.Next(first, "one", nextOpen: true, multiple: true);

        Assert.Equal(["two"], first);
        Assert.Equal(["two", "one"], second);
    }

    [Fact(DisplayName = "keeps an array item value intact while multiple")]
    [Trait("Requirement", "ACC-STA-06")]
    public void Keeps_an_array_item_value_intact_while_multiple()
    {
        string[] itemValue = ["section", "details"];

        IReadOnlyList<string[]> next = AccordionValues.Next(AccordionValues.Empty<string[]>(), itemValue, nextOpen: true, multiple: true);

        Assert.Same(itemValue, Assert.Single(next));
        Assert.True(AccordionValues.Contains(next, itemValue));
        // Compared by reference: an equal array is another identity.
        Assert.False(AccordionValues.Contains(next, ["section", "details"]));
    }

    [Fact(DisplayName = "multiple items can be open when `multiple = true`")]
    [Trait("Requirement", "ACC-STA-06")]
    public void Multiple_items_can_be_open_when_multiple_is_true()
    {
        IReadOnlyList<string> both = AccordionValues.Next(AccordionValues.Next(AccordionValues.Empty<string>(), "a", true, true), "b", true, true);
        IReadOnlyList<string> closed = AccordionValues.Next(both, "a", nextOpen: false, multiple: true);

        Assert.Equal(["a", "b"], both);
        Assert.Equal(["b"], closed);
        // Every commit is a new list; the one it came from is not changed.
        Assert.NotSame(both, closed);
        Assert.Equal(["a", "b"], both);
    }

    [Fact(DisplayName = "port: membership is SameValueZero while the single-mode check and the removal are strict, so NaN opens but is never removed")]
    [Trait("Requirement", "ACC-EDGE-10")]
    public void Membership_is_SameValueZero_while_the_single_mode_check_and_the_removal_are_strict()
    {
        IReadOnlyList<double> opened = AccordionValues.Next(AccordionValues.Empty<double>(), double.NaN, nextOpen: true, multiple: true);
        Assert.True(AccordionValues.Contains(opened, double.NaN));

        // Removal keeps NaN (NaN !== NaN), and the single-mode check never matches it.
        Assert.Equal([double.NaN], AccordionValues.Next(opened, double.NaN, nextOpen: false, multiple: true));
        Assert.Equal([double.NaN], AccordionValues.Next(opened, double.NaN, nextOpen: true, multiple: false));

        // 0 and -0 are one identity for membership and for strict equality alike.
        Assert.True(AccordionValues.Contains<double>([0.0], -0.0));
        Assert.Empty(AccordionValues.Next<double>([0.0], -0.0, nextOpen: false, multiple: true));
    }

    [Fact(DisplayName = "port: items sharing one identity are open together, and a close removes every occurrence")]
    [Trait("Requirement", "ACC-EDGE-11")]
    public void Items_sharing_one_identity_are_open_together_and_a_close_removes_every_occurrence()
    {
        IReadOnlyList<string> value = ["a", "b", "a"];

        Assert.True(AccordionValues.Contains(value, "a"));
        Assert.Equal(["b"], AccordionValues.Next(value, "a", nextOpen: false, multiple: true));
    }

    [Fact(DisplayName = "port: in single mode a request replaces a value of several items")]
    [Trait("Requirement", "ACC-EDGE-12")]
    public void In_single_mode_a_request_replaces_a_value_of_several_items()
    {
        IReadOnlyList<string> value = ["a", "b"];

        // The first listed item closes everything; another listed, open item stays open alone.
        Assert.Empty(AccordionValues.Next(value, "a", nextOpen: false, multiple: false));
        Assert.Equal(["b"], AccordionValues.Next(value, "b", nextOpen: false, multiple: false));
    }

    [Fact(DisplayName = "port: a null identity never matches a null in the value, because the item replaces it with its generated id")]
    [Trait("Requirement", "ACC-EDGE-09")]
    public void A_null_identity_never_matches_a_null_in_the_value()
    {
        // ACC-EDGE-09: the item's identity is its generated id when its value is null, so a root value of [null] opens
        // nothing; the identity the item uses is never null under string.
        Assert.Equal("base-ui-7", AccordionValues.Identity<string?>(valueSupplied: true, value: null, fallback: "base-ui-7"));
        Assert.Equal("base-ui-7", AccordionValues.Identity<string?>(valueSupplied: false, value: "ignored", fallback: "base-ui-7"));
        Assert.Equal("one", AccordionValues.Identity<string?>(valueSupplied: true, value: "one", fallback: "base-ui-7"));
        Assert.False(AccordionValues.Contains<string?>([null], AccordionValues.Identity<string?>(true, null, "base-ui-7")));
    }

    [Fact(DisplayName = "port: an item without a value under a TValue other than string has no identity")]
    [Trait("Requirement", "ACC-EDGE-09")]
    [Trait("Requirement", "ACC-API-10")]
    public void An_item_without_a_value_under_a_TValue_other_than_string_has_no_identity()
    {
        Assert.Equal(3, AccordionValues.Identity(valueSupplied: true, value: 3, fallback: "base-ui-1"));
        Assert.Equal(2, AccordionValues.Identity<int?>(valueSupplied: true, value: 2, fallback: "base-ui-1"));

        InvalidOperationException missing = Assert.Throws<InvalidOperationException>(() => AccordionValues.Identity<int?>(valueSupplied: true, value: null, fallback: "base-ui-1"));
        Assert.Equal(AccordionValues.MissingValueMessage, missing.Message);
        Assert.Throws<InvalidOperationException>(() => AccordionValues.Identity(valueSupplied: false, value: 0, fallback: "base-ui-1"));
        Assert.StartsWith("Blazix: ", AccordionValues.MissingValueMessage, StringComparison.Ordinal);
    }
}
