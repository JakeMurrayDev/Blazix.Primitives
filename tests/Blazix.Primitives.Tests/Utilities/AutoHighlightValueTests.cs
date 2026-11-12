// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// <see cref="AutoHighlightValue"/> at L1: Base UI's <c>autoHighlight: boolean | 'always'</c> as a hand-written union on
/// <see cref="ModalValue"/>'s shape (track 10's design point 4 (a), settled by the orchestrator 2026-11-04), with an implicit
/// conversion from <see langword="bool"/> and the literal as <see cref="AutoHighlightValue.Always"/>.
/// </summary>
public sealed class AutoHighlightValueTests
{
    [Fact(DisplayName = "holds a boolean or the always literal and reads each case without the other")]
    [Trait("Requirement", "MNU-API-55")]
    public void Holds_a_boolean_or_the_always_literal_and_reads_each_case_without_the_other()
    {
        AutoHighlightValue enabled = true;
        AutoHighlightValue always = AutoHighlightValue.Always;

        Assert.True(enabled.HasValue);
        Assert.True(enabled.TryGetValue(out bool value));
        Assert.True(value);
        Assert.False(enabled.TryGetValue(out AutoHighlightAlways _));
        Assert.False(enabled.IsAlways);
        Assert.True(always.HasValue);
        Assert.True(always.TryGetValue(out AutoHighlightAlways _));
        Assert.False(always.TryGetValue(out bool _));
        Assert.True(always.IsAlways);
        Assert.Equal(true, enabled.Value);
        Assert.IsType<AutoHighlightAlways>(always.Value);
    }

    [Fact(DisplayName = "holds false as a value, distinct from the default instance, and neither is enabled")]
    [Trait("Requirement", "MNU-API-55")]
    public void Holds_false_as_a_value_distinct_from_the_default_instance()
    {
        AutoHighlightValue off = false;
        AutoHighlightValue none = default;

        Assert.True(off.HasValue);
        Assert.True(off.TryGetValue(out bool value));
        Assert.False(value);
        Assert.False(off.IsEnabled);
        Assert.False(none.HasValue);
        Assert.Null(none.Value);
        Assert.False(none.IsEnabled);
        Assert.False(none.TryGetValue(out bool _));
        Assert.False(none.TryGetValue(out AutoHighlightAlways _));
        Assert.True(((AutoHighlightValue)true).IsEnabled);
        Assert.True(AutoHighlightValue.Always.IsEnabled);
    }

    [Fact(DisplayName = "a switch over the case types matches the boolean and the literal")]
    [Trait("Requirement", "MNU-API-55")]
    public void A_switch_over_the_case_types_matches_the_boolean_and_the_literal()
    {
        static string Describe(AutoHighlightValue autoHighlight) => autoHighlight switch
        {
            bool b => b ? "on" : "off",
            AutoHighlightAlways => "always",
        };

        Assert.Equal("on", Describe(true));
        Assert.Equal("off", Describe(false));
        Assert.Equal("always", Describe(AutoHighlightValue.Always));
    }
}
