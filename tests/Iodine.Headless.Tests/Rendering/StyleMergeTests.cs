using Blazix.Primitives.Rendering;

namespace Blazix.Primitives.Tests.Rendering;

/// <summary>
/// The style merge of ADR 02 decision 2 and RND-DOM-16: inline style strings are parsed into declarations (split on
/// <c>;</c> outside parentheses and quotes) and merged by property name, later keys winning, first-seen order kept,
/// so the merged string is a literal. No Base UI title covers the parser; the titles here are the port's.
/// </summary>
public sealed class StyleMergeTests
{
    [Fact(DisplayName = "keeps the position at which a property was first seen and appends new ones")]
    [Trait("Requirement", "RND-DOM-16")]
    public void Keeps_the_position_at_which_a_property_was_first_seen_and_appends_new_ones()
    {
        Assert.Equal("padding: 10px; color: rgb(255, 0, 0);", MergeProps.MergeStyles("padding: 10px", "color: rgb(255, 0, 0)"));
        Assert.Equal("color: red; background-color: blue;", MergeProps.MergeStyles("color: blue; background-color: blue", "color: red"));
    }

    [Fact(DisplayName = "splits declarations on semicolons outside parentheses and quotes")]
    [Trait("Requirement", "RND-DOM-16")]
    public void Splits_declarations_on_semicolons_outside_parentheses_and_quotes()
    {
        string? merged = MergeProps.MergeStyles(
            "background: url(\"a;b.png\"); font-family: 'x;y', serif; color: blue",
            "color: red; --shadow: calc(1px + 2px);");

        Assert.Equal("background: url(\"a;b.png\"); font-family: 'x;y', serif; color: red; --shadow: calc(1px + 2px);", merged);
    }

    [Fact(DisplayName = "returns a single operand as given and null for none")]
    [Trait("Requirement", "RND-ALG-06")]
    public void Returns_a_single_operand_as_given_and_null_for_none()
    {
        Assert.Equal("color: red", MergeProps.MergeStyles(null, "color: red"));
        Assert.Equal("color: red", MergeProps.MergeStyles("color: red", null));
        Assert.Equal("color: red", MergeProps.MergeStyles("color: red", ""));
        Assert.Null(MergeProps.MergeStyles(null, null));
        Assert.Null(MergeProps.MergeStyles("", ""));
    }

    [Fact(DisplayName = "trims whitespace and skips empty declarations")]
    [Trait("Requirement", "RND-DOM-16")]
    public void Trims_whitespace_and_skips_empty_declarations()
    {
        Assert.Equal("color: red; margin: 0;", MergeProps.MergeStyles("  color:red ;; ", " margin : 0 ; "));
    }

    [Fact(DisplayName = "mergeClassNames puts theirs first and skips empty operands")]
    [Trait("Requirement", "RND-ALG-05")]
    public void MergeClassNames_puts_theirs_first_and_skips_empty_operands()
    {
        Assert.Equal("theirs ours", MergeProps.MergeClassNames("ours", "theirs"));
        Assert.Equal("theirs", MergeProps.MergeClassNames(null, "theirs"));
        Assert.Equal("theirs", MergeProps.MergeClassNames("", "theirs"));
        Assert.Equal("ours", MergeProps.MergeClassNames("ours", null));
        Assert.Equal("ours", MergeProps.MergeClassNames("ours", ""));
        Assert.Null(MergeProps.MergeClassNames(null, null));
    }
}
