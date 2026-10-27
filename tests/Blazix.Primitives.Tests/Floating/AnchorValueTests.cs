using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests;

/// <summary>
/// L1: <see cref="AnchorValue"/> (ADR 08 Amendments 2026-10-27), a hand-written union (ADR 05 §4) of an element, an
/// element id and a virtual rect, with value equality that allocates nothing (ADR 16 rule 7). Base UI's function form
/// has no case: a changed anchor is a new value.
/// </summary>
public sealed class AnchorValueTests
{
    [Fact(DisplayName = "holds an element, an element id or a virtual anchor and reads each case without the others")]
    public void Holds_each_case_and_reads_it_without_the_others()
    {
        AnchorValue element = new ElementReference("anchor-1");
        AnchorValue id = "anchor-2";
        AnchorValue rect = new VirtualAnchor(10, 20, 30, 40);

        Assert.True(element.TryGetValue(out ElementReference reference));
        Assert.Equal("anchor-1", reference.Id);
        Assert.False(element.TryGetValue(out string? _));
        Assert.False(element.TryGetValue(out VirtualAnchor? _));

        Assert.True(id.TryGetValue(out string? elementId));
        Assert.Equal("anchor-2", elementId);
        Assert.False(id.TryGetValue(out ElementReference _));

        Assert.True(rect.TryGetValue(out VirtualAnchor? virtualAnchor));
        Assert.Equal(new VirtualAnchor(10, 20, 30, 40), virtualAnchor);
        Assert.False(rect.TryGetValue(out string? _));

        Assert.All([element, id, rect], value => Assert.True(value.HasValue));
    }

    [Fact(DisplayName = "the default anchor, a null id and a null virtual anchor hold no value")]
    public void The_default_and_null_cases_hold_no_value()
    {
        AnchorValue none = default;
        AnchorValue nullId = (string?)null;
        AnchorValue nullRect = (VirtualAnchor?)null;

        Assert.All([none, nullId, nullRect], value =>
        {
            Assert.False(value.HasValue);
            Assert.Null(value.Value);
            Assert.False(value.TryGetValue(out string? _));
        });
        Assert.Equal(none, nullId);
        Assert.Equal(none, nullRect);
    }

    [Fact(DisplayName = "two anchors are equal when they hold the same case and value")]
    public void Two_anchors_are_equal_when_they_hold_the_same_case_and_value()
    {
        Assert.Equal(new AnchorValue(new ElementReference("a")), new AnchorValue(new ElementReference("a")));
        Assert.True(new AnchorValue("a") == new AnchorValue("a"));
        Assert.True(new AnchorValue(new VirtualAnchor(1, 2, 3, 4)) == new AnchorValue(new VirtualAnchor(1, 2, 3, 4)));

        Assert.NotEqual(new AnchorValue(new ElementReference("a")), new AnchorValue("a"));
        Assert.True(new AnchorValue("a") != new AnchorValue("b"));
        Assert.True(new AnchorValue(new VirtualAnchor(1, 2, 3, 4)) != new AnchorValue(new VirtualAnchor(1, 2, 3, 5)));
        Assert.Equal(new AnchorValue("a").GetHashCode(), new AnchorValue("a").GetHashCode());
        Assert.False(new AnchorValue("a").Equals((object)"a"));
    }

    [Fact(DisplayName = "building, reading and comparing anchors allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public void Building_reading_and_comparing_anchors_allocates_nothing()
    {
        ElementReference reference = new("anchor");
        VirtualAnchor rect = new(1, 2, 3, 4);
        VirtualAnchor sameRect = new(1, 2, 3, 4);
        EqualityComparer<AnchorValue> comparer = EqualityComparer<AnchorValue>.Default;
        long total = 0;
        for (int i = 0; i < 2; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            AnchorValue fromElement = reference;
            AnchorValue fromId = "anchor";
            AnchorValue fromRect = rect;
            AnchorValue? nullable = sameRect;
            bool read = fromElement.TryGetValue(out ElementReference _) & fromId.TryGetValue(out string? _) & fromRect.TryGetValue(out VirtualAnchor? _);
            bool equal = comparer.Equals(fromRect, nullable!.Value) & fromId == new AnchorValue("anchor") & !fromElement.Equals(fromId);
            int hash = fromRect.GetHashCode() ^ fromId.GetHashCode() ^ fromElement.GetHashCode();
            total = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(read);
            Assert.True(equal);
            Assert.NotEqual(0, hash | 1);
        }

        Assert.Equal(0, total);
    }
}
