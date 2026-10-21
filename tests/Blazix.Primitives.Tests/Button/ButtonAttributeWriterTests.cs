using Blazix.Primitives.Rendering;

// Family tests use the project's root namespace: a `Button` namespace would hide the `Button` type.
namespace Blazix.Primitives.Tests;

/// <summary>
/// UBT-ALG-03 at L1 (docs/architecture/button-behavior.md §5): the attribute matrix of the button mechanism on the C#
/// writer every button-like part calls, one row per matrix row. The expected values are the matrix as the document
/// states it (D = disabled, F = focusableWhenDisabled, T = 0), not a second derivation of the algorithm. Titles are the
/// blocks of <c>Button.test.tsx</c> the document cites; the composite rows are the ones Toggle (M3) and composite
/// items (M2) will call.
/// </summary>
public sealed class ButtonAttributeWriterTests
{
    // The cells of the matrix: null means the attribute is absent.
    public static TheoryData<bool, bool, bool?, bool, string?, bool, string?> FocusableUnspecifiedOrFalseRows => new()
    {
        // native, composite, F, D, tabindex, disabled attribute, aria-disabled
        { true, false, null, false, "0", false, null },
        { true, false, false, false, "0", false, null },
        { true, false, null, true, "0", true, null },
        { true, false, false, true, "0", true, null },
        { true, true, null, false, null, false, "false" },
        { true, true, null, true, null, true, "true" },
        { true, true, false, false, null, false, null },
        { true, true, false, true, null, true, null },
        { false, false, null, false, "0", false, null },
        { false, false, false, false, "0", false, null },
        { false, false, null, true, "-1", false, "true" },
        { false, false, false, true, "-1", false, "true" },
        { false, true, null, false, null, false, null },
        { false, true, false, false, null, false, null },
        { false, true, null, true, null, false, "true" },
        { false, true, false, true, null, false, "true" },
    };

    public static TheoryData<bool, bool, bool?, bool, string?, bool, string?> FocusableTrueRows => new()
    {
        { true, false, true, false, "0", false, "false" },
        { true, false, true, true, "0", false, "true" },
        { true, true, true, false, null, false, "false" },
        { true, true, true, true, null, false, "true" },
        { false, false, true, false, "0", false, null },
        { false, false, true, true, "0", false, "true" },
        { false, true, true, false, null, false, null },
        { false, true, true, true, null, false, "true" },
    };

    [Theory(DisplayName = "prop: disabled")]
    [MemberData(nameof(FocusableUnspecifiedOrFalseRows))]
    [Trait("Requirement", "UBT-ALG-03")]
    public void Prop_disabled(bool native, bool composite, bool? focusableWhenDisabled, bool disabled, string? tabIndex, bool disabledAttribute, string? ariaDisabled)
    {
        AssertRow(native, composite, focusableWhenDisabled, disabled, tabIndex, disabledAttribute, ariaDisabled);
    }

    [Theory(DisplayName = "prop: focusableWhenDisabled")]
    [MemberData(nameof(FocusableTrueRows))]
    [Trait("Requirement", "UBT-ALG-03")]
    public void Prop_focusableWhenDisabled(bool native, bool composite, bool? focusableWhenDisabled, bool disabled, string? tabIndex, bool disabledAttribute, string? ariaDisabled)
    {
        AssertRow(native, composite, focusableWhenDisabled, disabled, tabIndex, disabledAttribute, ariaDisabled);
    }

    [Theory(DisplayName = "native mode adds type=\"button\" and no role; non-native mode adds role=\"button\" and no type")]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [Trait("Requirement", "UBT-DOM-01")]
    [Trait("Requirement", "UBT-DOM-02")]
    public void Native_mode_adds_type_button_and_no_role_non_native_mode_adds_role_button_and_no_type(bool native, bool composite)
    {
        AttributeList attributes = Write(native, disabled: false, focusableWhenDisabled: null, composite);

        Assert.Equal(native ? "button" : null, Text(attributes, "type"));
        Assert.Equal(native ? null : "button", Text(attributes, "role"));
    }

    [Theory(DisplayName = "the tabIndex option is the emitted tabindex outside composite mode")]
    [InlineData(true, false, null, "3")]
    [InlineData(false, false, null, "3")]
    [InlineData(false, true, true, "3")]
    [InlineData(false, true, false, "-1")]
    [Trait("Requirement", "UBT-DOM-03")]
    [Trait("Requirement", "UBT-DOM-04")]
    public void The_tabIndex_option_is_the_emitted_tabindex_outside_composite_mode(bool native, bool disabled, bool? focusableWhenDisabled, string expected)
    {
        AttributeList attributes = new();

        ButtonAttributeWriter.Write(attributes, native, disabled, focusableWhenDisabled, composite: false, tabIndex: 3);

        Assert.Equal(expected, Text(attributes, "tabindex"));
    }

    [Fact(DisplayName = "the writer adds only the five button attributes")]
    [Trait("Requirement", "UBT-ALG-03")]
    public void The_writer_adds_only_the_five_button_attributes()
    {
        AttributeList attributes = Write(native: true, disabled: true, focusableWhenDisabled: true, composite: false);

        Assert.All(attributes, entry => Assert.Contains(entry.Name, new[] { "type", "role", "tabindex", "disabled", "aria-disabled" }));
    }

    private static void AssertRow(bool native, bool composite, bool? focusableWhenDisabled, bool disabled, string? tabIndex, bool disabledAttribute, string? ariaDisabled)
    {
        AttributeList attributes = Write(native, disabled, focusableWhenDisabled, composite);

        Assert.Equal(tabIndex, Text(attributes, "tabindex"));
        Assert.Equal(disabledAttribute, attributes.TryGetValue("disabled", out AttributeValue value) && value.TryGetValue(out bool present) && present);
        Assert.Equal(ariaDisabled, Text(attributes, "aria-disabled"));
    }

    private static AttributeList Write(bool native, bool disabled, bool? focusableWhenDisabled, bool composite)
    {
        AttributeList attributes = new();
        ButtonAttributeWriter.Write(attributes, native, disabled, focusableWhenDisabled, composite);
        return attributes;
    }

    private static string? Text(AttributeList attributes, string name)
    {
        return attributes.TryGetValue(name, out AttributeValue value) && value.TryGetValue(out string text) ? text : null;
    }
}
