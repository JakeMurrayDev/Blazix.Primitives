// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// <see cref="MatchValue"/> at L1: a hand-written union of <see langword="bool"/> and <see cref="ValidityFlag"/> (ADR 05
/// §4), the type of <see cref="FieldError.Match"/> (FLD-API-30), that allocates nothing to build, convert or read (ADR 16
/// rule 7).
/// </summary>
public sealed class MatchValueTests
{
    [Fact(DisplayName = "holds a boolean or a validity flag and reads each case without the other")]
    [Trait("Requirement", "FLD-API-30")]
    public void Holds_a_boolean_or_a_validity_flag_and_reads_each_case_without_the_other()
    {
        MatchValue always = true;
        MatchValue flag = ValidityFlag.ValueMissing;

        Assert.True(always.HasValue);
        Assert.True(always.TryGetValue(out bool value));
        Assert.True(value);
        Assert.False(always.TryGetValue(out ValidityFlag _));
        Assert.True(flag.TryGetValue(out ValidityFlag held));
        Assert.Equal(ValidityFlag.ValueMissing, held);
        Assert.False(flag.TryGetValue(out bool _));
        Assert.Equal(true, always.Value);
        Assert.Equal(ValidityFlag.ValueMissing, flag.Value);
    }

    [Fact(DisplayName = "holds false as a value, distinct from the default instance")]
    [Trait("Requirement", "FLD-API-30")]
    public void Holds_false_as_a_value_distinct_from_the_default_instance()
    {
        MatchValue never = false;
        MatchValue none = default;

        Assert.True(never.HasValue);
        Assert.True(never.TryGetValue(out bool value));
        Assert.False(value);
        Assert.False(none.HasValue);
        Assert.Null(none.Value);
        Assert.False(none.TryGetValue(out bool _));
        Assert.False(none.TryGetValue(out ValidityFlag _));
    }

    [Fact(DisplayName = "names the eleven ValidityState flags")]
    [Trait("Requirement", "FLD-API-30")]
    public void Names_the_eleven_ValidityState_flags()
    {
        Assert.Equal(
            ["BadInput", "CustomError", "PatternMismatch", "RangeOverflow", "RangeUnderflow", "StepMismatch", "TooLong", "TooShort", "TypeMismatch", "Valid", "ValueMissing"],
            Enum.GetNames<ValidityFlag>());
    }

    [Fact(DisplayName = "building, converting and reading a match value allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public void Building_converting_and_reading_a_match_value_allocates_nothing()
    {
        long total = 0;
        for (int i = 0; i < 2; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            MatchValue fromBool = new(true);
            MatchValue fromFlag = new(ValidityFlag.TooShort);
            MatchValue converted = ValidityFlag.CustomError;
            MatchValue? nullable = false;
            bool read = fromBool.TryGetValue(out bool always) & fromFlag.TryGetValue(out ValidityFlag flag) & converted.HasValue & nullable.HasValue;
            total = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(read);
            Assert.True(always);
            Assert.Equal(ValidityFlag.TooShort, flag);
        }

        Assert.Equal(0, total);
    }
}
