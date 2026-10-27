// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// <see cref="ModalValue"/> at L1: Base UI's <c>modal: boolean | 'trap-focus'</c> as a hand-written union (ADR 08 §3's
/// boolean-or-literal row, Amendments 2026-10-27; the shape verified by issue 10), with an implicit conversion from
/// <see langword="bool"/> and the literal as <see cref="ModalValue.TrapFocus"/>, that allocates nothing (ADR 16 rule 7).
/// </summary>
public sealed class ModalValueTests
{
    [Fact(DisplayName = "holds a boolean or the trap-focus literal and reads each case without the other")]
    public void Holds_a_boolean_or_the_trap_focus_literal_and_reads_each_case_without_the_other()
    {
        ModalValue modal = true;
        ModalValue trapFocus = ModalValue.TrapFocus;

        Assert.True(modal.HasValue);
        Assert.True(modal.TryGetValue(out bool value));
        Assert.True(value);
        Assert.False(modal.TryGetValue(out ModalTrapFocus _));
        Assert.False(modal.IsTrapFocus);
        Assert.True(trapFocus.HasValue);
        Assert.True(trapFocus.TryGetValue(out ModalTrapFocus _));
        Assert.False(trapFocus.TryGetValue(out bool _));
        Assert.True(trapFocus.IsTrapFocus);
        Assert.Equal(true, modal.Value);
        Assert.IsType<ModalTrapFocus>(trapFocus.Value);
    }

    [Fact(DisplayName = "holds false as a value, distinct from the default instance")]
    public void Holds_false_as_a_value_distinct_from_the_default_instance()
    {
        ModalValue notModal = false;
        ModalValue none = default;

        Assert.True(notModal.HasValue);
        Assert.True(notModal.TryGetValue(out bool value));
        Assert.False(value);
        Assert.False(none.HasValue);
        Assert.Null(none.Value);
        Assert.False(none.TryGetValue(out bool _));
        Assert.False(none.TryGetValue(out ModalTrapFocus _));
    }

    [Fact(DisplayName = "a switch over the case types matches the boolean and the literal")]
    public void A_switch_over_the_case_types_matches_the_boolean_and_the_literal()
    {
        static string Describe(ModalValue modal) => modal switch
        {
            bool b => b ? "modal" : "non-modal",
            ModalTrapFocus => "trap-focus",
        };

        Assert.Equal("modal", Describe(true));
        Assert.Equal("non-modal", Describe(false));
        Assert.Equal("trap-focus", Describe(ModalValue.TrapFocus));
    }

    [Fact(DisplayName = "building, converting and reading a modal value allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public void Building_converting_and_reading_a_modal_value_allocates_nothing()
    {
        long total = 0;
        for (int i = 0; i < 2; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            ModalValue fromBool = new(true);
            ModalValue literal = ModalValue.TrapFocus;
            ModalValue converted = false;
            ModalValue? nullable = literal;
            bool read = fromBool.TryGetValue(out bool modal) & literal.IsTrapFocus & converted.HasValue & nullable.HasValue;
            total = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(read);
            Assert.True(modal);
        }

        Assert.Equal(0, total);
    }
}
