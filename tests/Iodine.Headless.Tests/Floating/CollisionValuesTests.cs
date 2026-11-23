using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests;

/// <summary>
/// L1: <see cref="CollisionBoundaryValue"/> and <see cref="CollisionPaddingValue"/> (ADR 08 Amendments 2026-10-27),
/// hand-written unions (ADR 05 §4) of Base UI's <c>collisionBoundary</c> (<c>'clipping-ancestors' | Element | Element[] |
/// Rect</c>) and <c>collisionPadding</c> (<c>number | Partial&lt;{ top, right, bottom, left }&gt;</c>), with value
/// equality that allocates nothing (ADR 16 rule 7).
/// </summary>
public sealed class CollisionValuesTests
{
    [Fact(DisplayName = "a collision boundary holds the clipping ancestors, an element, a list of elements or a rect")]
    public void A_collision_boundary_holds_each_case()
    {
        CollisionBoundaryValue clipping = CollisionBoundaryValue.ClippingAncestors;
        CollisionBoundaryValue element = new ElementReference("boundary");
        CollisionBoundaryValue elements = new ElementReference[] { new("a"), new("b") };
        CollisionBoundaryValue rect = new Rect(0, 0, 300, 200);

        Assert.True(clipping.HasValue);
        Assert.True(clipping.IsClippingAncestors);
        Assert.False(clipping.TryGetValue(out ElementReference _));

        Assert.True(element.TryGetValue(out ElementReference reference));
        Assert.Equal("boundary", reference.Id);
        Assert.False(element.IsClippingAncestors);

        Assert.True(elements.TryGetValue(out IReadOnlyList<ElementReference>? list));
        Assert.Equal(["a", "b"], list.Select(item => item.Id));
        Assert.False(elements.TryGetValue(out Rect? _));

        Assert.True(rect.TryGetValue(out Rect? value));
        Assert.Equal(new Rect(0, 0, 300, 200), value);

        CollisionBoundaryValue none = default;
        Assert.False(none.HasValue);
        Assert.False(none.IsClippingAncestors);
    }

    [Fact(DisplayName = "two collision boundaries are equal when they hold the same case and value, lists compared item by item")]
    public void Two_collision_boundaries_compare_by_value()
    {
        Assert.Equal(CollisionBoundaryValue.ClippingAncestors, CollisionBoundaryValue.ClippingAncestors);
        Assert.True(new CollisionBoundaryValue(new ElementReference("a")) == new CollisionBoundaryValue(new ElementReference("a")));
        Assert.True(new CollisionBoundaryValue(new Rect(1, 2, 3, 4)) == new CollisionBoundaryValue(new Rect(1, 2, 3, 4)));
        Assert.True(
            new CollisionBoundaryValue(new ElementReference[] { new("a"), new("b") })
            == new CollisionBoundaryValue(new List<ElementReference> { new("a"), new("b") }));

        Assert.True(new CollisionBoundaryValue(new ElementReference[] { new("a"), new("b") }) != new CollisionBoundaryValue(new ElementReference[] { new("b"), new("a") }));
        Assert.True(new CollisionBoundaryValue(new ElementReference[] { new("a") }) != new CollisionBoundaryValue(new ElementReference("a")));
        Assert.NotEqual(CollisionBoundaryValue.ClippingAncestors, default);
        Assert.Equal(
            new CollisionBoundaryValue(new ElementReference[] { new("a") }).GetHashCode(),
            new CollisionBoundaryValue(new List<ElementReference> { new("a") }).GetHashCode());
    }

    [Fact(DisplayName = "a collision padding holds a number or per-side values, missing sides being zero")]
    public void A_collision_padding_holds_a_number_or_sides()
    {
        CollisionPaddingValue all = 12;
        CollisionPaddingValue sides = new PaddingSides(Top: 4, Left: 8);

        Assert.True(all.TryGetValue(out double padding));
        Assert.Equal(12, padding);
        Assert.False(all.TryGetValue(out PaddingSides? _));

        Assert.True(sides.TryGetValue(out PaddingSides? value));
        Assert.Equal(new PaddingSides(4, 0, 0, 8), value);
        Assert.False(sides.TryGetValue(out double _));

        CollisionPaddingValue none = default;
        Assert.False(none.HasValue);
        Assert.True(new CollisionPaddingValue(5) == 5d);
        Assert.True(new CollisionPaddingValue(5) != new CollisionPaddingValue(new PaddingSides(5, 5, 5, 5)));
        Assert.True(new CollisionPaddingValue(new PaddingSides(1, 2, 3, 4)) == new CollisionPaddingValue(new PaddingSides(1, 2, 3, 4)));
    }

    [Fact(DisplayName = "building, reading and comparing collision values allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public void Building_reading_and_comparing_collision_values_allocates_nothing()
    {
        ElementReference[] references = [new("a"), new("b")];
        ElementReference[] sameReferences = [new("a"), new("b")];
        Rect rect = new(1, 2, 3, 4);
        PaddingSides paddingSides = new(1, 2, 3, 4);
        long total = 0;
        for (int i = 0; i < 2; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            CollisionBoundaryValue list = references;
            CollisionBoundaryValue sameList = sameReferences;
            CollisionBoundaryValue fromRect = rect;
            CollisionBoundaryValue clipping = CollisionBoundaryValue.ClippingAncestors;
            CollisionPaddingValue number = 5;
            CollisionPaddingValue sides = paddingSides;
            bool equal = list == sameList & fromRect != clipping & EqualityComparer<CollisionPaddingValue>.Default.Equals(number, 5) & sides != number;
            bool read = list.TryGetValue(out IReadOnlyList<ElementReference>? _) & number.TryGetValue(out double _) & sides.TryGetValue(out PaddingSides? _);
            int hash = list.GetHashCode() ^ fromRect.GetHashCode() ^ clipping.GetHashCode() ^ number.GetHashCode() ^ sides.GetHashCode();
            total = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(equal);
            Assert.True(read);
            Assert.NotEqual(0, hash | 1);
        }

        Assert.Equal(0, total);
    }
}
