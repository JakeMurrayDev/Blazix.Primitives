namespace Blazix.Primitives.Tests;

/// <summary>
/// L1: <see cref="OffsetValue"/>, <see cref="Offset"/> and <see cref="OffsetOverride"/> (ADR 08 Amendments 2026-10-27;
/// the fields settled by the orchestrator 2026-10-27 as the positioning track's design point 2): a number, or the
/// declarative form of Base UI's offset function, compared by value with the overrides item by item, allocating nothing.
/// </summary>
public sealed class OffsetValueTests
{
    [Fact(DisplayName = "an offset holds a number or a declarative offset and reads each case without the other")]
    public void An_offset_holds_a_number_or_a_declarative_offset()
    {
        OffsetValue number = 7;
        OffsetValue form = new Offset(AnchorWidth: 1, PositionerWidth: 1);

        Assert.True(number.TryGetValue(out double value));
        Assert.Equal(7, value);
        Assert.False(number.TryGetValue(out Offset? _));
        Assert.True(form.TryGetValue(out Offset? offset));
        Assert.Equal(new Offset(0, 1, 0, 1, 0), offset);
        Assert.False(form.TryGetValue(out double _));

        OffsetValue none = default;
        OffsetValue nullForm = (Offset?)null;
        Assert.False(none.HasValue);
        Assert.False(nullForm.HasValue);
        Assert.Equal(none, nullForm);
    }

    [Fact(DisplayName = "two declarative offsets are equal when their terms and their overrides, in order, are equal")]
    public void Two_declarative_offsets_compare_by_value()
    {
        Offset first = new(Constant: 2) { Overrides = [new OffsetOverride(Side.Right, null, Constant: 10), new OffsetOverride(null, Align.End, PositionerWidth: 1)] };
        Offset same = new(Constant: 2) { Overrides = new List<OffsetOverride> { new(Side.Right, null, Constant: 10), new(null, Align.End, PositionerWidth: 1) } };
        Offset reordered = new(Constant: 2) { Overrides = [new OffsetOverride(null, Align.End, PositionerWidth: 1), new OffsetOverride(Side.Right, null, Constant: 10)] };

        Assert.Equal(first, same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, reordered);
        Assert.NotEqual(first, first with { Overrides = null });
        Assert.NotEqual(first, first with { Constant = 3 });
        Assert.True(new OffsetValue(first) == new OffsetValue(same));
        Assert.True(new OffsetValue(5) != new OffsetValue(new Offset(Constant: 5)));
    }

    [Fact(DisplayName = "building, reading and comparing offsets allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public void Building_reading_and_comparing_offsets_allocates_nothing()
    {
        Offset first = new(Constant: 2) { Overrides = [new OffsetOverride(Side.Right, Align.Start, Constant: 10)] };
        Offset same = new(Constant: 2) { Overrides = [new OffsetOverride(Side.Right, Align.Start, Constant: 10)] };
        long total = 0;
        for (int i = 0; i < 2; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            OffsetValue number = 3;
            OffsetValue form = first;
            OffsetValue? nullable = same;
            bool read = number.TryGetValue(out double _) & form.TryGetValue(out Offset? _);
            bool equal = form == nullable!.Value & number != form & first.Equals(same) & EqualityComparer<OffsetValue>.Default.Equals(form, same);
            int hash = form.GetHashCode() ^ number.GetHashCode();
            total = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(read);
            Assert.True(equal);
            Assert.NotEqual(0, hash | 1);
        }

        Assert.Equal(0, total);
    }
}
