// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// <see cref="Duration"/> at L1: a hand-written union of milliseconds and <see cref="TimeSpan"/> (ADR 05 §4) that
/// allocates nothing to build, convert or read (ADR 16 rule 7).
/// </summary>
public sealed class DurationTests
{
    [Fact(DisplayName = "holds milliseconds or a time span and reads each case without the other")]
    public void Holds_milliseconds_or_a_time_span_and_reads_each_case_without_the_other()
    {
        Duration milliseconds = 300;
        Duration span = TimeSpan.FromSeconds(1.5);

        Assert.True(milliseconds.TryGetValue(out int ms));
        Assert.Equal(300, ms);
        Assert.False(milliseconds.TryGetValue(out TimeSpan _));
        Assert.True(span.TryGetValue(out TimeSpan value));
        Assert.Equal(TimeSpan.FromSeconds(1.5), value);
        Assert.False(span.TryGetValue(out int _));
        Assert.Equal(300, milliseconds.ToMilliseconds());
        Assert.Equal(1500, span.ToMilliseconds());
    }

    [Fact(DisplayName = "the default duration holds no value and is zero milliseconds")]
    public void The_default_duration_holds_no_value_and_is_zero_milliseconds()
    {
        Duration none = default;

        Assert.False(none.HasValue);
        Assert.Null(none.Value);
        Assert.False(none.TryGetValue(out int _));
        Assert.False(none.TryGetValue(out TimeSpan _));
        Assert.Equal(0, none.ToMilliseconds());
    }

    [Fact(DisplayName = "building, converting and reading a duration allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public void Building_converting_and_reading_a_duration_allocates_nothing()
    {
        long total = 0;
        for (int i = 0; i < 2; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Duration fromInt = new(300);
            Duration fromSpan = new(TimeSpan.FromMilliseconds(300));
            Duration converted = 250;
            Duration? nullable = TimeSpan.FromSeconds(1);
            bool read = fromInt.TryGetValue(out int ms) & fromSpan.TryGetValue(out TimeSpan span) & converted.HasValue & nullable.HasValue;
            double sum = fromInt.ToMilliseconds() + fromSpan.ToMilliseconds() + converted.ToMilliseconds() + (nullable?.ToMilliseconds() ?? 0) + ms + span.TotalMilliseconds;
            total = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(read);
            Assert.Equal(2450, sum);
        }

        Assert.Equal(0, total);
    }
}
